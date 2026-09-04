namespace Ordering.Worker.Cdc;

/// <summary>
/// Raised when the stored checkpoint is older than the CDC retention window:
/// changes after the checkpoint may already have been cleaned up, so messages
/// could be lost. This must never be skipped silently — it requires an operator
/// decision (reconcile from the OutboxMessages table, then reset the checkpoint).
/// </summary>
public sealed class CdcCheckpointExpiredException(string checkpointLsn, string minRetainedLsn)
    : Exception(
        $"Outbox CDC checkpoint {checkpointLsn} is older than the minimum retained LSN {minRetainedLsn}. " +
        "CDC cleanup may have discarded unpublished changes. Reconcile against dbo.OutboxMessages, " +
        "re-publish missing events manually if needed, then reset the checkpoint (see Ordering.Worker README).")
{
    public string CheckpointLsn { get; } = checkpointLsn;
    public string MinRetainedLsn { get; } = minRetainedLsn;
}

/// <summary>
/// Raised when CDC is not (yet) usable: database not CDC-enabled or the
/// OutboxMessages capture instance does not exist.
/// </summary>
public sealed class CdcNotReadyException(string message) : Exception(message);
