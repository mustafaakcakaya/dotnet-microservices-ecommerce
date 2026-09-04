using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Publishing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Ordering.Worker.Cdc;
using Ordering.Worker.Checkpoints;
using Ordering.Worker.Failures;
using Ordering.Worker.Processing;

namespace Ordering.IntegrationTests;

/// <summary>
/// End-to-end against real SQL Server CDC: an order saved through the
/// DbContext produces an outbox row, the CDC capture job picks it up, the
/// worker's processor reads it and hands it to a (fake) publisher, and the
/// checkpoint only advances when publishing succeeds.
/// </summary>
[Collection(nameof(SqlServerCdcCollection))]
public sealed class CdcPipelineTests(SqlServerCdcFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan CaptureTimeout = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    private readonly OutboxCdcOptions _options = new() { BatchSize = 50, MaxPublishAttempts = 10 };

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await TestOrders.CleanupAsync(context);
    }

    [Fact]
    public async Task Worker_ReadsCdcInsert_PublishesIt_AndAdvancesCheckpointOnlyOnSuccess()
    {
        var checkpointStore = new SqlOutboxCheckpointStore(fixture.ConnectionFactory, Options.Create(_options));
        var publisher = new RecordingPublisher();
        var processor = CreateProcessor(checkpointStore, publisher);

        // Drain changes left over from earlier tests so assertions are deterministic.
        await DrainAsync(processor);

        await using var context = fixture.CreateDbContext();
        var (customer, product) = await TestOrders.SeedReferenceDataAsync(context);
        var order = TestOrders.NewOrder(customer.Id, product.Id);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var outboxId = (await context.OutboxMessages.SingleAsync(
            m => m.AggregateId == order.Id.Value.ToString())).Id;

        // 1) The CDC capture job eventually surfaces the insert and it gets published.
        var published = await WaitForAsync(processor,
            () => publisher.Published.SingleOrDefault(m => m.Id == outboxId) is not null);
        Assert.True(published, "CDC change was not captured and published within the timeout.");

        var record = publisher.Published.Single(m => m.Id == outboxId);
        Assert.Equal("ordering.order-created", record.EventType);
        Assert.Equal(order.Id.Value.ToString(), record.AggregateId);
        Assert.DoesNotContain(TestOrders.CardNumber, record.Payload);

        // 2) Checkpoint advanced after the successful publish...
        var checkpoint = await checkpointStore.GetAsync(CancellationToken.None);
        Assert.NotNull(checkpoint);

        // ...and the same message is not delivered again on the next cycle.
        var again = await processor.ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(OutboxProcessingStatus.Idle, again.Status);
        Assert.Single(publisher.Published, m => m.Id == outboxId);

        // 3) Broker failure: the checkpoint must not move past the failing message.
        var secondOrder = TestOrders.NewOrder(customer.Id, product.Id);
        context.Orders.Add(secondOrder);
        await context.SaveChangesAsync();

        publisher.Fail = true;
        var faulted = await WaitForAsync(processor,
            () => publisher.FailedAttempts > 0);
        Assert.True(faulted, "Broker failure was not observed within the timeout.");
        Assert.Equal(checkpoint, await checkpointStore.GetAsync(CancellationToken.None));

        // 4) Broker recovers: the same message is retried and the checkpoint advances.
        publisher.Fail = false;
        var secondOutboxId = (await context.OutboxMessages.SingleAsync(
            m => m.AggregateId == secondOrder.Id.Value.ToString())).Id;

        var retried = await WaitForAsync(processor,
            () => publisher.Published.Any(m => m.Id == secondOutboxId));
        Assert.True(retried, "Message was not re-published after the broker recovered.");

        var advanced = await checkpointStore.GetAsync(CancellationToken.None);
        Assert.NotNull(advanced);
        Assert.True(Lsn.Compare(advanced!, checkpoint!) > 0);
    }

    private OutboxCdcProcessor CreateProcessor(
        IOutboxCheckpointStore checkpointStore, IIntegrationEventPublisher publisher) => new(
        new SqlCdcOutboxReader(fixture.ConnectionFactory),
        checkpointStore,
        new SqlOutboxPublishFailureStore(fixture.ConnectionFactory),
        publisher,
        Options.Create(_options),
        NullLogger<OutboxCdcProcessor>.Instance);

    private static async Task DrainAsync(OutboxCdcProcessor processor)
    {
        using var timeout = new CancellationTokenSource(CaptureTimeout);
        while (true)
        {
            try
            {
                if ((await processor.ProcessOnceAsync(timeout.Token)).Status == OutboxProcessingStatus.Idle)
                {
                    return;
                }
            }
            catch (CdcNotReadyException)
            {
                // Capture job not started yet; keep waiting.
            }

            await Task.Delay(PollDelay, timeout.Token);
        }
    }

    /// <summary>Polls the processor until <paramref name="condition"/> holds or the timeout expires.</summary>
    private static async Task<bool> WaitForAsync(
        OutboxCdcProcessor processor, Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(CaptureTimeout);
        while (!timeout.IsCancellationRequested)
        {
            try
            {
                await processor.ProcessOnceAsync(timeout.Token);
            }
            catch (CdcNotReadyException)
            {
            }

            if (condition())
            {
                return true;
            }

            try
            {
                await Task.Delay(PollDelay, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        return condition();
    }

    private sealed class RecordingPublisher : IIntegrationEventPublisher
    {
        public List<IntegrationEventEnvelope> Published { get; } = [];
        public bool Fail { get; set; }
        public int FailedAttempts { get; private set; }

        public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        {
            if (Fail)
            {
                FailedAttempts++;
                throw new InvalidOperationException("Broker unavailable (simulated)");
            }

            Published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
