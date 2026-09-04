using BuildingBlocks.Messaging.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ordering.Worker.Cdc;
using Ordering.Worker.Processing;

namespace Ordering.Worker.UnitTests.Processing;

public sealed class OutboxCdcProcessorTests
{
    private readonly FakeCdcOutboxReader _reader = new();
    private readonly FakeCheckpointStore _checkpointStore = new();
    private readonly FakeFailureStore _failureStore = new();
    private readonly FakePublisher _publisher = new();
    private readonly OutboxCdcOptions _options = new() { MaxPublishAttempts = 3 };

    private OutboxCdcProcessor CreateProcessor() => new(
        _reader,
        _checkpointStore,
        _failureStore,
        _publisher,
        Options.Create(_options),
        NullLogger<OutboxCdcProcessor>.Instance);

    [Fact]
    public async Task ProcessOnce_EmptyBatch_ReturnsIdle()
    {
        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxProcessingStatus.Idle, result.Status);
        Assert.Empty(_checkpointStore.Saves);
    }

    [Fact]
    public async Task ProcessOnce_SuccessfulBatch_PublishesAndAdvancesCheckpointPerGroup()
    {
        var group1 = Group(lsn: 1, Message(), Message());
        var group2 = Group(lsn: 2, Message());
        _reader.OnRead = _ => new CdcBatch([group1, group2]);

        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxProcessingStatus.Processed, result.Status);
        Assert.Equal(3, result.Published);
        Assert.Equal(3, _publisher.Published.Count);
        Assert.Equal(2, _checkpointStore.Saves.Count);
        Assert.Equal(group2.Lsn, _checkpointStore.Current);
    }

    [Fact]
    public async Task ProcessOnce_PublishFails_CheckpointNotAdvanced()
    {
        var message = Message();
        _reader.OnRead = _ => new CdcBatch([Group(1, message)]);
        _publisher.ShouldFail = _ => true;

        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxProcessingStatus.Faulted, result.Status);
        Assert.Null(_checkpointStore.Current);
        Assert.Empty(_checkpointStore.Saves);
        Assert.Equal(1, _failureStore.AttemptsFor(message.Id));
    }

    [Fact]
    public async Task ProcessOnce_SecondGroupFails_CheckpointStopsAtFirstGroup()
    {
        var failing = Message();
        var group1 = Group(1, Message());
        var group2 = Group(2, failing);
        _reader.OnRead = _ => new CdcBatch([group1, group2]);
        _publisher.ShouldFail = m => m.Id == failing.Id;

        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxProcessingStatus.Faulted, result.Status);
        Assert.Equal(group1.Lsn, _checkpointStore.Current);
    }

    [Fact]
    public async Task ProcessOnce_FailingMessage_IsRetriedUntilPoisonedThenStreamContinues()
    {
        var failing = Message();
        var healthy = Message();
        _reader.OnRead = _ => new CdcBatch([Group(1, failing, healthy)]);
        _publisher.ShouldFail = m => m.Id == failing.Id;

        var processor = CreateProcessor();

        // Attempts 1 and 2: cycle faults, checkpoint stays put, message will be re-read.
        for (var attempt = 1; attempt < _options.MaxPublishAttempts; attempt++)
        {
            var faulted = await processor.ProcessOnceAsync(CancellationToken.None);
            Assert.Equal(OutboxProcessingStatus.Faulted, faulted.Status);
            Assert.Equal(attempt, _failureStore.AttemptsFor(failing.Id));
            Assert.Null(_checkpointStore.Current);
        }

        // Attempt 3 reaches MaxPublishAttempts: poisoned, rest of the group publishes.
        var result = await processor.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxProcessingStatus.Processed, result.Status);
        Assert.Equal(1, result.Poisoned);
        Assert.Equal(1, result.Published);
        Assert.Equal(healthy.Id, Assert.Single(_publisher.Published).Id);
        Assert.NotNull(_checkpointStore.Current);
    }

    [Fact]
    public async Task ProcessOnce_PoisonedMessage_IsSkippedAndCheckpointAdvances()
    {
        var poisoned = Message();
        var healthy = Message();
        _failureStore.SetPoisoned(poisoned.Id);
        _reader.OnRead = _ => new CdcBatch([Group(1, poisoned, healthy)]);

        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(OutboxProcessingStatus.Processed, result.Status);
        Assert.Equal(healthy.Id, Assert.Single(_publisher.Published).Id);
        Assert.NotNull(_checkpointStore.Current);
    }

    [Fact]
    public async Task ProcessOnce_ReadsFromStoredCheckpoint()
    {
        byte[]? observedCheckpoint = null;
        _checkpointStore.Current = LsnOf(42);
        _reader.OnRead = afterLsn =>
        {
            observedCheckpoint = afterLsn;
            return CdcBatch.Empty;
        };

        await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(LsnOf(42), observedCheckpoint);
    }

    [Fact]
    public async Task ProcessOnce_CancelledToken_ThrowsAndDoesNotAdvanceCheckpoint()
    {
        _reader.OnRead = _ => new CdcBatch([Group(1, Message())]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateProcessor().ProcessOnceAsync(cts.Token));

        Assert.Empty(_publisher.Published);
        Assert.Empty(_checkpointStore.Saves);
    }

    private static IntegrationEventEnvelope Message() => new(
        Guid.NewGuid(),
        "ordering.order-created",
        SchemaVersion: 1,
        AggregateId: Guid.NewGuid().ToString(),
        CorrelationId: null,
        OccurredOnUtc: DateTime.UtcNow,
        Payload: "{}");

    private static CdcLsnGroup Group(byte lsn, params IntegrationEventEnvelope[] messages) =>
        new(LsnOf(lsn), messages);

    private static byte[] LsnOf(byte value)
    {
        var lsn = new byte[Lsn.Length];
        lsn[^1] = value;
        return lsn;
    }
}
