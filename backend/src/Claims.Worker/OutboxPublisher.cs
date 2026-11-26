using Claims.Infrastructure.Outbox;

namespace Claims.Worker;

/// <summary>
/// Polls the outbox every second and publishes unsent rows to claims.events. A full batch is followed at once by the
/// next; failures (Kafka or database down) back off exponentially up to 30 s, and the loop never dies on an error.
/// </summary>
public sealed class OutboxPublisher(IServiceScopeFactory scopes, TimeProvider clock, ILogger<OutboxPublisher> logger)
    : BackgroundService
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var failures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<OutboxDispatcher>().PublishBatchAsync(stoppingToken);

                if (result.Failed > 0)
                {
                    delay = Backoff(++failures);
                    logger.LogWarning("Outbox: {Sent} sent, {Failed} failed to reach Kafka; retrying in {Delay}", result.Sent, result.Failed, delay);
                }
                else
                {
                    if (failures > 0)
                    {
                        logger.LogInformation("Outbox: publishing again after {Failures} failed attempt(s)", failures);
                    }

                    if (result.Sent > 0)
                    {
                        logger.LogInformation("Outbox: {Sent} event(s) published", result.Sent);
                    }

                    failures = 0;
                    delay = result.WasFull ? TimeSpan.Zero : PollInterval;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Database unreachable, or Kafka refused the message outright. Keep going: this loop is the only publisher.
                delay = Backoff(++failures);
                logger.LogWarning(ex, "Outbox: publish failed; retrying in {Delay}", delay);
            }

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, clock, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>1 s, 2 s, 4 s … capped at <see cref="MaxBackoff"/>.</summary>
    internal static TimeSpan Backoff(int failures) =>
        TimeSpan.FromSeconds(Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, Math.Min(failures, 10) - 1)));
}
