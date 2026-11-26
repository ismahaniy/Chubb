using System.Text.Json;
using Claims.Domain;
using Claims.Infrastructure.Outbox;
using static Claims.UnitTests.Domain.ClaimTestData;

namespace Claims.UnitTests.Infrastructure;

public sealed class OutboxMessageFactoryTests
{
    [Fact]
    public void Every_domain_event_type_has_an_outbox_payload()
    {
        var eventTypes = typeof(IDomainEvent).Assembly.GetTypes()
            .Where(t => typeof(IDomainEvent).IsAssignableFrom(t) && t is { IsClass: true, IsAbstract: false })
            .ToHashSet();

        // Drive one claim through every event, then check each one maps.
        var claim = SubmittedClaim();
        claim.Assign(OfficerId, Now);
        claim.RequestInformation(OfficerId, "Photos?", Now);
        claim.ProvideInformation(OpenRequestId(claim), ClaimantId, "Sent.", Now);
        claim.RecordAssessment(OfficerId, Money(2_000m), null, Now);
        claim.Approve(OfficerId, Money(1_800m), "Covered", Now);
        claim.Settle(OfficerId, Now);
        var rejected = ClaimIn(ClaimStatus.UnderReview);
        rejected.Reject(OfficerId, "Excluded peril", Now);
        IDomainEvent[] events = [.. claim.DomainEvents, .. rejected.DomainEvents];

        events.Select(e => e.GetType()).ToHashSet().SetEquals(eventTypes).ShouldBeTrue("a domain event type is not exercised here");
        foreach (var domainEvent in events)
        {
            var message = OutboxMessageFactory.Create(domainEvent, "corr-1");

            message.Id.ShouldBe(domainEvent.EventId);
            message.ClaimId.ShouldBe(domainEvent.ClaimId);
            message.EventType.ShouldBe(domainEvent.GetType().Name);
            message.CorrelationId.ShouldBe("corr-1");
        }
    }

    [Fact]
    public void Payload_is_the_explicit_contract_without_envelope_fields()
    {
        var submitted = SubmittedClaim().DomainEvents.OfType<ClaimSubmitted>().Single();

        var message = OutboxMessageFactory.Create(submitted, null);

        using var payload = JsonDocument.Parse(message.Payload);
        payload.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(["claimantId", "policyId", "claimedAmount"], ignoreOrder: true);
        var amount = payload.RootElement.GetProperty("claimedAmount");
        amount.GetProperty("amount").GetDecimal().ShouldBe(3_000m);
        amount.GetProperty("currency").GetString().ShouldBe(Currency);
        amount.EnumerateObject().Count().ShouldBe(2); // no IsZero leaking from the domain Money type
    }
}
