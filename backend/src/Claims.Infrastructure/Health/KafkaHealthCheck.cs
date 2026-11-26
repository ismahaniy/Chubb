using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Claims.Infrastructure.Health;

/// <summary>Asks the broker for cluster metadata through the shared admin client.</summary>
internal sealed class KafkaHealthCheck(IAdminClient adminClient) : IHealthCheck
{
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            // GetMetadata is synchronous and bounded by its own timeout.
            var metadata = await Task.Run(() => adminClient.GetMetadata(MetadataTimeout), cancellationToken);
            return metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "No Kafka brokers available.");
        }
        catch (KafkaException ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Kafka is unreachable.", ex);
        }
    }
}
