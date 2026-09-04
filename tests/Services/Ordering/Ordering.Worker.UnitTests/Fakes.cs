using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Publishing;
using Ordering.Worker.Cdc;
using Ordering.Worker.Checkpoints;
using Ordering.Worker.Failures;
using Ordering.Worker.Initialization;

namespace Ordering.Worker.UnitTests;

internal sealed class FakeCdcOutboxReader : ICdcOutboxReader
{
    public Func<byte[]?, CdcBatch> OnRead { get; set; } = _ => CdcBatch.Empty;

    public Task<CdcBatch> ReadBatchAsync(byte[]? afterLsn, int batchSize, CancellationToken cancellationToken) =>
        Task.FromResult(OnRead(afterLsn));
}

internal sealed class FakeCheckpointStore : IOutboxCheckpointStore
{
    public byte[]? Current { get; set; }
    public List<byte[]> Saves { get; } = [];

    public Task<byte[]?> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task SaveAsync(byte[] lsn, CancellationToken cancellationToken)
    {
        Current = lsn;
        Saves.Add(lsn);
        return Task.CompletedTask;
    }
}

internal sealed class FakeFailureStore : IOutboxPublishFailureStore
{
    private readonly Dictionary<Guid, (int Attempts, DateTime? PoisonedOnUtc)> _failures = [];

    public Task<OutboxPublishFailure?> GetAsync(Guid messageId, CancellationToken cancellationToken) =>
        Task.FromResult(_failures.TryGetValue(messageId, out var failure)
            ? new OutboxPublishFailure(failure.Attempts, failure.PoisonedOnUtc)
            : null);

    public Task<int> RecordFailureAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        var attempts = _failures.TryGetValue(messageId, out var failure) ? failure.Attempts + 1 : 1;
        _failures[messageId] = (attempts, _failures.GetValueOrDefault(messageId).PoisonedOnUtc);
        return Task.FromResult(attempts);
    }

    public Task MarkPoisonedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        var attempts = _failures.GetValueOrDefault(messageId).Attempts;
        _failures[messageId] = (attempts, DateTime.UtcNow);
        return Task.CompletedTask;
    }

    public void SetPoisoned(Guid messageId) => _failures[messageId] = (1, DateTime.UtcNow);

    public int AttemptsFor(Guid messageId) => _failures.GetValueOrDefault(messageId).Attempts;
}

internal sealed class FakePublisher : IIntegrationEventPublisher
{
    public List<IntegrationEventEnvelope> Published { get; } = [];
    public Func<IntegrationEventEnvelope, bool> ShouldFail { get; set; } = _ => false;

    public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        if (ShouldFail(envelope))
        {
            throw new InvalidOperationException("Broker unavailable (simulated)");
        }

        Published.Add(envelope);
        return Task.CompletedTask;
    }
}

internal sealed class FakeCdcReadinessWaiter : ICdcReadinessWaiter
{
    public Task WaitUntilReadyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
