using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BuildingBlocks.Messaging.Publishing;

/// <summary>
/// Readiness probe for the Kafka connection. Queries cluster metadata through the
/// existing producer handle instead of publishing a probe message, so health
/// checks never pollute real topics.
/// </summary>
public sealed class KafkaHealthCheck(IProducer<string, string> producer) : IHealthCheck
{
    private static readonly TimeSpan MetadataTimeout = TimeSpan.FromSeconds(5);

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var adminClient = new DependentAdminClientBuilder(producer.Handle).Build();
            var metadata = adminClient.GetMetadata(MetadataTimeout);

            return Task.FromResult(metadata.Brokers.Count > 0
                ? HealthCheckResult.Healthy($"{metadata.Brokers.Count} broker(s) reachable")
                : HealthCheckResult.Unhealthy("No Kafka brokers reachable"));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Kafka metadata request failed", exception));
        }
    }
}
