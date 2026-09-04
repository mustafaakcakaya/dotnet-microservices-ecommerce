namespace Ordering.Worker.Checkpoints;

/// <summary>
/// Persists the last successfully processed CDC LSN per consumer.
/// The checkpoint is only advanced after every message in an LSN group has been
/// published (or explicitly poisoned), which yields at-least-once delivery:
/// a crash between publish and checkpoint write re-delivers the group.
/// </summary>
public interface IOutboxCheckpointStore
{
    Task<byte[]?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(byte[] lsn, CancellationToken cancellationToken);
}
