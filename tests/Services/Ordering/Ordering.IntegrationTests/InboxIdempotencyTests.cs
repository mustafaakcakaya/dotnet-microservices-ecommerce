using BuildingBlocks.Messaging.Events.Ordering.V1;
using BuildingBlocks.Messaging.Inbox;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ordering.IntegrationTests;

/// <summary>
/// The publisher delivers at-least-once, so consumers must be idempotent.
/// Verifies that redelivering the same MessageId runs the consumer's work only
/// once, using the real SQL Server inbox store.
/// </summary>
[Collection(nameof(SqlServerCdcCollection))]
public sealed class InboxIdempotencyTests(SqlServerCdcFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await using var context = fixture.CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM dbo.InboxMessages;");
    }

    [Fact]
    public async Task DuplicateDelivery_RunsConsumerOnce()
    {
        var counter = new ConsumeCounter();
        var inbox = new CountingInboxStore(new SqlServerInboxStore(fixture.ConnectionString));

        await using var provider = new ServiceCollection()
            .AddSingleton(counter)
            .AddSingleton<IInboxStore>(inbox)
            .AddMassTransitTestHarness(config =>
            {
                config.AddConsumer<CountingConsumer>();
                config.UsingInMemory((context, configurator) =>
                {
                    configurator.UseConsumeFilter(typeof(IdempotentConsumeFilter<>), context);
                    configurator.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var messageId = Guid.NewGuid();
            var integrationEvent = new OrderCreatedIntegrationEvent
            {
                OrderId = Guid.NewGuid(),
                CustomerId = Guid.NewGuid(),
                OrderName = "ORD_1",
                Status = "Pending",
                TotalPrice = 1000
            };

            // Same message id twice: exactly what a crash between publish and
            // checkpoint write produces in the CDC worker.
            await harness.Bus.Publish(integrationEvent, context => context.MessageId = messageId);
            await harness.Bus.Publish(integrationEvent, context => context.MessageId = messageId);

            // Waiting on the inbox itself is deterministic. The harness's Consumed
            // observer cannot be used here: the filter short-circuits the duplicate
            // before it reaches a consumer, so the second delivery is never observed.
            await WaitForClaimAttemptsAsync(inbox, expected: 2);

            Assert.Equal(1, counter.Invocations);
            Assert.Equal(1, inbox.RejectedClaims);

            await using var dbContext = fixture.CreateDbContext();
            var inboxRow = Assert.Single(await dbContext.InboxMessages
                .Where(message => message.MessageId == messageId)
                .ToListAsync());
            Assert.NotNull(inboxRow.ProcessedOnUtc);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private static async Task WaitForClaimAttemptsAsync(CountingInboxStore inbox, int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (inbox.ClaimAttempts < expected)
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Only {inbox.ClaimAttempts} of {expected} deliveries reached the inbox before the timeout.");
            }

            await Task.Delay(50);
        }
    }

    /// <summary>
    /// Wraps the real store so the test can tell how many deliveries reached the
    /// inbox and how many of them were rejected as duplicates.
    /// </summary>
    private sealed class CountingInboxStore(IInboxStore inner) : IInboxStore
    {
        private int _claimAttempts;
        private int _rejectedClaims;

        public int ClaimAttempts => Volatile.Read(ref _claimAttempts);

        public int RejectedClaims => Volatile.Read(ref _rejectedClaims);

        public async Task<bool> TryBeginAsync(Guid messageId, string consumerName, CancellationToken cancellationToken)
        {
            var proceed = await inner.TryBeginAsync(messageId, consumerName, cancellationToken);

            if (!proceed)
            {
                Interlocked.Increment(ref _rejectedClaims);
            }

            Interlocked.Increment(ref _claimAttempts);
            return proceed;
        }

        public Task CompleteAsync(Guid messageId, string consumerName, CancellationToken cancellationToken) =>
            inner.CompleteAsync(messageId, consumerName, cancellationToken);
    }

    private sealed class ConsumeCounter
    {
        private int _invocations;

        public int Invocations => Volatile.Read(ref _invocations);

        public void Increment() => Interlocked.Increment(ref _invocations);
    }

    private sealed class CountingConsumer(ConsumeCounter counter) : IConsumer<OrderCreatedIntegrationEvent>
    {
        public Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
        {
            counter.Increment();
            return Task.CompletedTask;
        }
    }
}
