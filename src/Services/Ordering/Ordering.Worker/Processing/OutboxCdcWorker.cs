using Microsoft.Extensions.Options;
using Ordering.Worker.Cdc;
using Ordering.Worker.Initialization;

namespace Ordering.Worker.Processing;

/// <summary>
/// Hosting shell around <see cref="OutboxCdcProcessor"/>: polling loop,
/// exponential backoff on failures and cooperative cancellation. All actual
/// work lives in the processor so it stays unit-testable.
/// </summary>
public sealed class OutboxCdcWorker(
    OutboxCdcProcessor processor,
    ICdcReadinessWaiter readinessWaiter,
    IOptions<OutboxCdcOptions> options,
    ILogger<OutboxCdcWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await readinessWaiter.WaitUntilReadyAsync(stoppingToken);

            await RunPollingLoopAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }

        logger.LogInformation("Outbox CDC worker stopped");
    }

    private async Task RunPollingLoopAsync(CancellationToken stoppingToken)
    {
        var consecutiveFailures = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;

            try
            {
                var result = await processor.ProcessOnceAsync(stoppingToken);
                consecutiveFailures = result.Status == OutboxProcessingStatus.Faulted
                    ? consecutiveFailures + 1
                    : 0;

                delay = result.Status switch
                {
                    // Drain immediately while there is work; back off on failure.
                    OutboxProcessingStatus.Processed => TimeSpan.Zero,
                    OutboxProcessingStatus.Faulted => Backoff(consecutiveFailures),
                    _ => options.Value.PollingInterval
                };
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (CdcCheckpointExpiredException exception)
            {
                // Never skipped silently: this needs an operator decision.
                consecutiveFailures++;
                logger.LogCritical(exception,
                    "Outbox CDC checkpoint is outside the retention window; events may have been lost. " +
                    "Manual reconciliation required — see Ordering.Worker README");
                delay = Backoff(consecutiveFailures);
            }
            catch (CdcNotReadyException exception)
            {
                consecutiveFailures++;
                logger.LogWarning("CDC not ready yet: {Reason}", exception.Message);
                delay = Backoff(consecutiveFailures);
            }
            catch (Exception exception)
            {
                consecutiveFailures++;
                logger.LogError(exception, "Outbox CDC processing cycle failed");
                delay = Backoff(consecutiveFailures);
            }

            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    private TimeSpan Backoff(int consecutiveFailures)
    {
        var baseDelayMs = options.Value.RetryBaseDelay.TotalMilliseconds;
        var maxDelayMs = options.Value.RetryMaxDelay.TotalMilliseconds;

        var exponent = Math.Min(consecutiveFailures - 1, 10);
        var delayMs = Math.Min(baseDelayMs * Math.Pow(2, exponent), maxDelayMs);

        // Up to 20% jitter so multiple instances don't retry in lockstep.
        delayMs += delayMs * 0.2 * Random.Shared.NextDouble();

        return TimeSpan.FromMilliseconds(delayMs);
    }
}
