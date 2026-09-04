using BuildingBlocks.Messaging.Publishing;
using Microsoft.Extensions.Options;
using Ordering.Worker.Cdc;
using Ordering.Worker.Checkpoints;
using Ordering.Worker.Failures;

namespace Ordering.Worker.Processing;

public enum OutboxProcessingStatus
{
    /// <summary>No new CDC changes.</summary>
    Idle,

    /// <summary>The whole batch was handled and the checkpoint advanced.</summary>
    Processed,

    /// <summary>A publish failed; the cycle stopped and the failing message will be retried.</summary>
    Faulted
}

public sealed record OutboxProcessingResult(OutboxProcessingStatus Status, int Published, int Poisoned);

/// <summary>
/// One poll cycle: read a batch of committed outbox INSERTs from CDC, publish
/// each to the broker and advance the checkpoint per completed LSN group.
/// If publishing fails, the checkpoint is NOT advanced past that group, so the
/// message is re-read on a later cycle — at-least-once delivery by design.
/// Payloads are intentionally never logged; only ids and contract names are.
/// </summary>
public sealed class OutboxCdcProcessor(
    ICdcOutboxReader reader,
    IOutboxCheckpointStore checkpointStore,
    IOutboxPublishFailureStore failureStore,
    IIntegrationEventPublisher publisher,
    IOptions<OutboxCdcOptions> options,
    ILogger<OutboxCdcProcessor> logger)
{
    public async Task<OutboxProcessingResult> ProcessOnceAsync(CancellationToken cancellationToken)
    {
        var checkpoint = await checkpointStore.GetAsync(cancellationToken);
        var batch = await reader.ReadBatchAsync(checkpoint, options.Value.BatchSize, cancellationToken);

        if (batch.Groups.Count == 0)
        {
            return new OutboxProcessingResult(OutboxProcessingStatus.Idle, 0, 0);
        }

        var published = 0;
        var poisoned = 0;

        foreach (var group in batch.Groups)
        {
            foreach (var message in group.Messages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var failure = await failureStore.GetAsync(message.Id, cancellationToken);
                if (failure?.IsPoisoned == true)
                {
                    logger.LogWarning(
                        "Skipping poisoned outbox message {MessageId} ({EventType} v{SchemaVersion})",
                        message.Id, message.EventType, message.SchemaVersion);
                    continue;
                }

                try
                {
                    await publisher.PublishAsync(message, cancellationToken);
                    published++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    var attempts = await failureStore.RecordFailureAsync(
                        message.Id, exception.Message, cancellationToken);

                    if (attempts >= options.Value.MaxPublishAttempts)
                    {
                        await failureStore.MarkPoisonedAsync(message.Id, cancellationToken);
                        poisoned++;
                        logger.LogError(exception,
                            "Outbox message {MessageId} ({EventType} v{SchemaVersion}, aggregate {AggregateId}) " +
                            "poisoned after {Attempts} attempts; the stream continues without it",
                            message.Id, message.EventType, message.SchemaVersion, message.AggregateId, attempts);
                        continue;
                    }

                    logger.LogWarning(exception,
                        "Publish failed for outbox message {MessageId} ({EventType} v{SchemaVersion}), " +
                        "attempt {Attempts}/{MaxAttempts}; checkpoint stays at {Checkpoint}",
                        message.Id, message.EventType, message.SchemaVersion,
                        attempts, options.Value.MaxPublishAttempts, Lsn.ToHex(checkpoint));

                    return new OutboxProcessingResult(OutboxProcessingStatus.Faulted, published, poisoned);
                }
            }

            // Every message in this LSN group is now published or poisoned;
            // only now is it safe to move the checkpoint forward.
            await checkpointStore.SaveAsync(group.Lsn, cancellationToken);
            checkpoint = group.Lsn;
        }

        logger.LogInformation(
            "Outbox CDC cycle published {Published} message(s) ({Poisoned} poisoned), checkpoint advanced to {Checkpoint}",
            published, poisoned, Lsn.ToHex(checkpoint));

        return new OutboxProcessingResult(OutboxProcessingStatus.Processed, published, poisoned);
    }
}
