using System.Text;
using Claims.Infrastructure;
using Claims.Infrastructure.Messaging;
using Claims.Infrastructure.Notifications;
using Confluent.Kafka;

namespace Claims.Worker;

/// <summary>
/// Consumes claims.events (group claims-worker) and writes notifications. The offset is committed only after the
/// database write; a redelivered event is skipped through processed_events. A message that fails
/// <see cref="MaxAttempts"/> times goes to claims.events.dlq with the error, then its offset is committed.
/// </summary>
public sealed class NotificationProjector(
    IServiceScopeFactory scopes,
    IServiceProvider services,
    IProducer<string, string> producer,
    TimeProvider clock,
    ILogger<NotificationProjector> logger) : BackgroundService
{
    public const int MaxAttempts = 3;

    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2)];
    private static readonly TimeSpan PauseAfterError = TimeSpan.FromSeconds(5);

    // Consume() blocks, so the loop gets a thread of its own instead of holding up host start-up.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Run(() => ConsumeAsync(stoppingToken), stoppingToken);

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = DependencyInjection.BootstrapServers(services),
            GroupId = KafkaTopics.ConsumerGroup,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
        })
            .SetLogHandler((_, message) => DependencyInjection.LogKafka(services, message))
            .Build();

        consumer.Subscribe(KafkaTopics.Events);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    await HandleAsync(consumer, result, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // E.g. the broker is unreachable or the topic is missing. Never let the loop die.
                    logger.LogWarning(ex, "Notifications: consume failed; retrying in {Delay}", PauseAfterError);
                    await DelayAsync(PauseAfterError, stoppingToken);
                }
            }
        }
        finally
        {
            consumer.Close(); // leave the group cleanly so partitions move to another member at once
        }
    }

    private async Task HandleAsync(IConsumer<string, string> consumer, ConsumeResult<string, string> result, CancellationToken stoppingToken)
    {
        using var _ = logger.BeginScope(new Dictionary<string, object?>
        {
            ["ClaimId"] = result.Message.Key,
            ["Partition"] = result.Partition.Value,
            ["Offset"] = result.Offset.Value,
        });

        Exception? lastError = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var envelope = EventEnvelope.Parse(result.Message.Value);
                using var eventScope = logger.BeginScope(new Dictionary<string, object?>
                {
                    ["EventId"] = envelope.EventId,
                    ["EventType"] = envelope.EventType,
                    ["CorrelationId"] = envelope.CorrelationId,
                });

                await using var scope = scopes.CreateAsyncScope();
                var projected = await scope.ServiceProvider.GetRequiredService<NotificationProjection>().ProjectAsync(envelope, stoppingToken);
                if (!projected)
                {
                    logger.LogInformation("Notifications: event already processed, skipped");
                }

                lastError = null;
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                lastError = ex;
                logger.LogWarning(ex, "Notifications: attempt {Attempt} of {MaxAttempts} failed", attempt, MaxAttempts);
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(RetryDelays[attempt - 1], clock, stoppingToken);
                }
            }
        }

        if (lastError is not null)
        {
            try
            {
                await DeadLetterAsync(result, lastError, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                // Not parked and not committed: rewind so this message is retried rather than skipped.
                logger.LogError(ex, "Notifications: dead-lettering failed; message will be retried");
                consumer.Seek(result.TopicPartitionOffset);
                await DelayAsync(PauseAfterError, stoppingToken);
                return;
            }
        }

        try
        {
            consumer.Commit(result);
        }
        catch (KafkaException ex)
        {
            // E.g. the partition moved in a rebalance. The work is saved; the redelivered copy is skipped by processed_events.
            logger.LogWarning(ex, "Notifications: offset commit failed; the event may be redelivered and will be skipped");
        }
    }

    private async Task DeadLetterAsync(ConsumeResult<string, string> result, Exception error, CancellationToken stoppingToken)
    {
        var headers = new Headers();
        foreach (var header in result.Message.Headers ?? [])
        {
            headers.Add(header.Key, header.GetValueBytes());
        }

        headers.Add("error", Encoding.UTF8.GetBytes(error.Message));
        headers.Add("error-type", Encoding.UTF8.GetBytes(error.GetType().FullName ?? error.GetType().Name));
        headers.Add("source-topic", Encoding.UTF8.GetBytes(result.Topic));
        headers.Add("source-partition", Encoding.UTF8.GetBytes(result.Partition.Value.ToString()));
        headers.Add("source-offset", Encoding.UTF8.GetBytes(result.Offset.Value.ToString()));
        headers.Add("attempts", Encoding.UTF8.GetBytes(MaxAttempts.ToString()));

        await producer.ProduceAsync(KafkaTopics.DeadLetter, new Message<string, string>
        {
            Key = result.Message.Key,
            Value = result.Message.Value,
            Headers = headers,
        }, stoppingToken);

        logger.LogError(error, "Notifications: event sent to {DeadLetterTopic} after {MaxAttempts} failed attempts", KafkaTopics.DeadLetter, MaxAttempts);
    }

    private async Task DelayAsync(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, clock, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
    }
}
