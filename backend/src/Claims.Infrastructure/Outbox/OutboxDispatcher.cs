using System.Text;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Persistence;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;

namespace Claims.Infrastructure.Outbox;

public sealed record OutboxBatchResult(int Sent, int Failed)
{
    public bool WasFull => Sent + Failed == OutboxDispatcher.BatchSize;
}

/// <summary>Publishes one batch of unsent outbox rows to Kafka. Called by the Worker's OutboxPublisher.</summary>
public sealed class OutboxDispatcher(ClaimsDbContext db, IProducer<string, string> producer, TimeProvider clock)
{
    public const int BatchSize = 100;

    /// <summary>
    /// Locks up to <see cref="BatchSize"/> unsent rows, oldest first (SKIP LOCKED: another publisher takes other rows),
    /// produces them, and commits sent_at for the ones Kafka acknowledged. A crash before the commit rolls back and
    /// releases the locks, so the rows are produced again on the next poll; consumers dedupe by eventId.
    /// </summary>
    public async Task<OutboxBatchResult> PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var messages = await db.OutboxMessages
            .FromSql($"""
                SELECT * FROM outbox_messages
                WHERE sent_at IS NULL
                ORDER BY occurred_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return new OutboxBatchResult(0, 0);
        }

        // Start every produce before awaiting any: one round trip for the batch instead of one per row. Started in
        // occurred_at order; with the idempotent producer, retries keep that order within a partition (one claim).
        var deliveries = messages.Select(m => (Message: m, Delivery: ProduceAsync(m))).ToList();

        var failed = 0;
        foreach (var (message, delivery) in deliveries)
        {
            try
            {
                await delivery;
                message.SentAt = clock.GetUtcNow();
            }
            catch (ProduceException<string, string> ex)
            {
                failed++;
                message.Attempts++;
                message.LastError = Truncate(ex.Error.ToString());
            }
        }

        // Not the request's token: the rows are already in Kafka, so record that even while the worker is stopping.
        await db.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);

        return new OutboxBatchResult(messages.Count - failed, failed);
    }

    private Task<DeliveryResult<string, string>> ProduceAsync(OutboxMessage message)
    {
        var headers = new Headers { { "eventType", Encoding.UTF8.GetBytes(message.EventType) } };
        if (message.CorrelationId is not null)
        {
            headers.Add("correlationId", Encoding.UTF8.GetBytes(message.CorrelationId));
        }

        // No cancellation token: cancelling ProduceAsync abandons the wait, not the send, so the row would be left
        // "unsent" although it may be delivered. MessageTimeoutMs bounds the wait instead.
        return producer.ProduceAsync(KafkaTopics.Events, new Message<string, string>
        {
            Key = message.ClaimId.ToString(),
            Value = EventEnvelope.From(message).ToJson(),
            Headers = headers,
        });
    }

    private static string Truncate(string error) => error.Length <= 2000 ? error : error[..2000];
}
