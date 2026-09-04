using BuildingBlocks.Messaging.Events;

namespace Ordering.Worker.Cdc;

/// <summary>
/// All outbox INSERTs that share one commit LSN (i.e. one source transaction).
/// The checkpoint only ever advances at group boundaries, so a group is either
/// fully published or fully retried.
/// </summary>
public sealed record CdcLsnGroup(byte[] Lsn, IReadOnlyList<IntegrationEventEnvelope> Messages);

public sealed record CdcBatch(IReadOnlyList<CdcLsnGroup> Groups)
{
    public static readonly CdcBatch Empty = new(Array.Empty<CdcLsnGroup>());

    public int MessageCount => Groups.Sum(group => group.Messages.Count);
}

public interface ICdcOutboxReader
{
    /// <summary>
    /// Reads committed OutboxMessages INSERTs from the CDC change table with an
    /// LSN strictly greater than <paramref name="afterLsn"/> (or from the start
    /// of retained changes when null). Returns only complete LSN groups.
    /// Throws <see cref="CdcCheckpointExpiredException"/> when the checkpoint is
    /// older than the CDC retention window (possible data loss — never skipped
    /// silently).
    /// </summary>
    Task<CdcBatch> ReadBatchAsync(byte[]? afterLsn, int batchSize, CancellationToken cancellationToken);
}
