using System.Globalization;
using Claims.Domain;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Outbox;
using Claims.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Claims.Infrastructure.Notifications;

/// <summary>Turns a claim event into an in-app notification, at most once per event. Called by the Worker's NotificationProjector.</summary>
public sealed class NotificationProjection(ClaimsDbContext db, TimeProvider clock)
{
    /// <summary>The consumer column of processed_events.</summary>
    public const string ConsumerName = "notifications";

    private const string ProcessedEventsPrimaryKey = "pk_processed_events";

    /// <summary>
    /// Returns false when the event was already handled. The processed_events row and the notification are saved in
    /// one SaveChanges (one transaction), so a crash leaves both or neither and redelivery is safe.
    /// </summary>
    public async Task<bool> ProjectAsync(EventEnvelope envelope, CancellationToken cancellationToken)
    {
        // Fast path for the usual redelivery. The primary key below is the real guarantee.
        if (await db.ProcessedEvents.AnyAsync(e => e.EventId == envelope.EventId && e.Consumer == ConsumerName, cancellationToken))
        {
            return false;
        }

        var claim = await db.Claims
            .AsNoTracking()
            .Where(c => c.Id == envelope.ClaimId)
            .Select(c => new ClaimRef(c.ClaimNumber, c.ClaimantId, c.AssignedOfficerId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Claim {envelope.ClaimId} of event {envelope.EventId} does not exist.");

        var now = clock.GetUtcNow();
        db.ProcessedEvents.Add(new ProcessedEvent { EventId = envelope.EventId, Consumer = ConsumerName, ProcessedAt = now });

        if (ToNotice(envelope, claim) is { } notice)
        {
            db.Notifications.Add(new Notification(notice.RecipientId, envelope.ClaimId, notice.Message, now));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: ProcessedEventsPrimaryKey,
        })
        {
            // Another consumer (e.g. after a rebalance) handled it between our check and our save; ours rolled back.
            return false;
        }
    }

    /// <summary>Who hears about the event, and what they read. Null when nobody needs telling.</summary>
    private static (Guid RecipientId, string Message)? ToNotice(EventEnvelope envelope, ClaimRef claim)
    {
        var number = claim.ClaimNumber;

        // Free text (question, reason) is left out: it can be longer than a notification, and the claim page shows it.
        return envelope.EventType switch
        {
            nameof(ClaimSubmitted) => (claim.ClaimantId, $"We received your claim {number}."),
            nameof(ClaimAssigned) => (claim.ClaimantId, $"Your claim {number} is now under review."),
            nameof(InformationRequested) => (claim.ClaimantId, $"We need more information for claim {number}. Open the claim to answer."),
            nameof(InformationProvided) when claim.AssignedOfficerId is { } officerId =>
                (officerId, $"The claimant answered your information request on claim {number}."),
            nameof(ClaimApproved) =>
                (claim.ClaimantId, $"Your claim {number} was approved for {Format(envelope.PayloadAs<ClaimApprovedPayload>().ApprovedAmount)}."),
            nameof(ClaimRejected) => (claim.ClaimantId, $"Your claim {number} was not approved. Open the claim to see the reason."),
            nameof(ClaimSettled) =>
                (claim.ClaimantId, $"Your claim {number} was settled: {Format(envelope.PayloadAs<ClaimSettledPayload>().SettledAmount)} paid."),
            // ClaimAssessed is internal (reserve), and unknown types are ignored so new events don't break this consumer.
            _ => null,
        };
    }

    private static string Format(MoneyPayload money) => $"{money.Currency} {money.Amount.ToString("N2", CultureInfo.InvariantCulture)}";

    private sealed record ClaimRef(string ClaimNumber, Guid ClaimantId, Guid? AssignedOfficerId);
}
