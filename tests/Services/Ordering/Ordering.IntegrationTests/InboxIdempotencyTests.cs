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

        await using var provider = new ServiceCollection()
            .AddSingleton(counter)
            .AddSingleton<IInboxStore>(new SqlServerInboxStore(fixture.ConnectionString))
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

            // Same message id twice: this is exactly what a crash between publish
            // and checkpoint write produces in the CDC worker.
            await harness.Bus.Publish(integrationEvent, context => context.MessageId = messageId);
            await harness.Bus.Publish(integrationEvent, context => context.MessageId = messageId);

            await harness.InactivityTask;

            Assert.Equal(1, counter.Invocations);

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
