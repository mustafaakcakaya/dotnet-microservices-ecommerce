using Microsoft.Extensions.Options;

namespace Basket.API.Outbox;

/// <summary>
/// Hosting shell around <see cref="BasketOutboxProcessor"/>: runs a cycle on every
/// tick and stops cleanly on shutdown. All the actual work - and its tests - live
/// in the processor.
/// </summary>
/// <remarks>
/// Each Basket.API instance runs its own relay. That is safe, not merely tolerated:
/// two relays may publish the same message, but it carries the same MessageId and
/// the consumer's inbox keeps only one.
/// </remarks>
public sealed class BasketOutboxRelay(
    BasketOutboxProcessor processor,
    IOptions<BasketOutboxOptions> options,
    ILogger<BasketOutboxRelay> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollingInterval);

        try
        {
            do
            {
                try
                {
                    await processor.ProcessOnceAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // A cycle that fails as a whole (database unreachable, say) is
                    // retried on the next tick; the messages themselves are untouched.
                    logger.LogError(exception, "Basket outbox relay cycle failed");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }
}
