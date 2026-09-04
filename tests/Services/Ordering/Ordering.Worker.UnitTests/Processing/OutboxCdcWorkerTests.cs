using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ordering.Worker.Cdc;
using Ordering.Worker.Processing;

namespace Ordering.Worker.UnitTests.Processing;

public sealed class OutboxCdcWorkerTests
{
    [Fact]
    public async Task Worker_StopsPromptly_WhenCancellationRequested()
    {
        var reader = new FakeCdcOutboxReader();
        var checkpointStore = new FakeCheckpointStore();
        var options = Options.Create(new OutboxCdcOptions
        {
            // Long polling interval: shutdown must interrupt the delay, not wait it out.
            PollingInterval = TimeSpan.FromMinutes(5)
        });

        var processor = new OutboxCdcProcessor(
            reader,
            checkpointStore,
            new FakeFailureStore(),
            new FakePublisher(),
            options,
            NullLogger<OutboxCdcProcessor>.Instance);

        var worker = new OutboxCdcWorker(
            processor,
            new FakeCdcReadinessWaiter(),
            options,
            NullLogger<OutboxCdcWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await worker.StopAsync(stopTimeout.Token);

        Assert.True(worker.ExecuteTask!.IsCompleted);
    }
}
