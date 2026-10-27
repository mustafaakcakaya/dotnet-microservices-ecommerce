using Basket.API.Outbox;
using BuildingBlocks.Messaging.Events;
using BuildingBlocks.Messaging.Events.Basket.V1;
using BuildingBlocks.Messaging.Publishing;
using Marten;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Basket.API.IntegrationTests;

[Collection(nameof(PostgresCollection))]
public sealed class OutboxRelayTests(PostgresFixture fixture) : IAsyncLifetime
{
    private readonly RecordingPublisher _publisher = new();
    private readonly BasketOutboxOptions _options = new() { MaxPublishAttempts = 3 };

    public Task InitializeAsync() => fixture.Store.Advanced.Clean.DeleteAllDocumentsAsync();

    public Task DisposeAsync() => fixture.Store.Advanced.Clean.DeleteAllDocumentsAsync();

    private BasketOutboxProcessor CreateProcessor(IIntegrationEventPublisher? publisher = null) => new(
        fixture.Store,
        publisher ?? _publisher,
        Options.Create(_options),
        TimeProvider.System,
        NullLogger<BasketOutboxProcessor>.Instance);

    [Fact]
    public async Task DueMessage_IsPublishedAndRemoved()
    {
        var message = await AddMessageAsync("mustafa");

        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.Published);
        Assert.Equal(message.Id, Assert.Single(_publisher.Published).Id);
        Assert.Empty(await PendingAsync());
    }

    [Fact]
    public async Task MessagesArePublishedOldestFirst()
    {
        var first = await AddMessageAsync("first", occurredOnUtc: DateTimeOffset.UtcNow.AddMinutes(-2));
        var second = await AddMessageAsync("second", occurredOnUtc: DateTimeOffset.UtcNow.AddMinutes(-1));

        await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal([first.Id, second.Id], _publisher.Published.Select(e => e.Id));
    }

    // A broker failure must leave the message in place to be retried later -
    // never lost, never published twice in the same cycle.
    [Fact]
    public async Task PublishFailure_KeepsMessageAndSchedulesRetry()
    {
        var message = await AddMessageAsync("mustafa");
        _publisher.FailWith = new InvalidOperationException("broker unavailable (simulated)");

        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.Failed);
        var stored = Assert.Single(await PendingAsync());
        Assert.Equal(message.Id, stored.Id);
        Assert.Equal(1, stored.Attempts);
        Assert.Contains("broker unavailable", stored.LastError);
        Assert.True(stored.NextAttemptOnUtc > DateTimeOffset.UtcNow);
        Assert.Null(stored.PoisonedOnUtc);
    }

    [Fact]
    public async Task MessageNotYetDue_IsLeftAlone()
    {
        await AddMessageAsync("mustafa", nextAttemptOnUtc: DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(0, result.Published);
        Assert.Empty(_publisher.Published);
        Assert.Single(await PendingAsync());
    }

    [Fact]
    public async Task RetriedMessage_IsPublishedOnceTheBrokerRecovers()
    {
        var message = await AddMessageAsync("mustafa");
        _publisher.FailWith = new InvalidOperationException("broker unavailable (simulated)");
        await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        // Broker is back, and the backoff has elapsed.
        _publisher.FailWith = null;
        await MakeDueAsync(message.Id);
        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.Published);
        Assert.Equal(message.Id, Assert.Single(_publisher.Published).Id);
        Assert.Empty(await PendingAsync());
    }

    // A message that keeps failing must not be retried forever, but it must not
    // vanish either: it stays in the outbox, marked, for someone to look at.
    [Fact]
    public async Task RepeatedFailure_PoisonsTheMessageAndStopsRetrying()
    {
        var message = await AddMessageAsync("mustafa");
        _publisher.FailWith = new InvalidOperationException("malformed");

        for (var attempt = 1; attempt <= _options.MaxPublishAttempts; attempt++)
        {
            await MakeDueAsync(message.Id);
            await CreateProcessor().ProcessOnceAsync(CancellationToken.None);
        }

        var poisoned = Assert.Single(await PendingAsync());
        Assert.NotNull(poisoned.PoisonedOnUtc);
        Assert.Equal(_options.MaxPublishAttempts, poisoned.Attempts);

        // Even once due and with a healthy broker, it is not picked up again.
        _publisher.FailWith = null;
        await MakeDueAsync(message.Id);
        var result = await CreateProcessor().ProcessOnceAsync(CancellationToken.None);
        Assert.Equal(0, result.Published);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task OneFailingMessage_DoesNotHoldUpTheOthers()
    {
        var bad = await AddMessageAsync("bad", occurredOnUtc: DateTimeOffset.UtcNow.AddMinutes(-2));
        var good = await AddMessageAsync("good", occurredOnUtc: DateTimeOffset.UtcNow.AddMinutes(-1));
        var publisher = new RecordingPublisher { FailFor = bad.Id };

        var result = await CreateProcessor(publisher).ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(1, result.Published);
        Assert.Equal(1, result.Failed);
        Assert.Equal(good.Id, Assert.Single(publisher.Published).Id);
    }

    // The shared publisher, through MassTransit: the envelope must come out as the
    // versioned contract Ordering consumes, with the outbox id as the MessageId -
    // the key Ordering's inbox deduplicates on.
    [Fact]
    public async Task SharedPublisher_SendsTheContractWithTheOutboxIdAsMessageId()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            var message = await AddMessageAsync("mustafa");
            var processor = CreateProcessor(
                new RabbitMqIntegrationEventPublisher(harness.Bus, new IntegrationEventTypeRegistry()));

            await processor.ProcessOnceAsync(CancellationToken.None);

            Assert.True(await harness.Published.Any<BasketCheckoutSubmittedIntegrationEvent>());
            var published = harness.Published.Select<BasketCheckoutSubmittedIntegrationEvent>().Single();
            Assert.Equal(message.Id, published.Context.MessageId);
            Assert.Equal("mustafa", published.Context.Message.UserName);
            Assert.Equal(100, published.Context.Message.TotalPrice);
        }
        finally
        {
            await harness.Stop();
        }
    }

    private async Task<BasketOutboxMessage> AddMessageAsync(
        string userName,
        DateTimeOffset? occurredOnUtc = null,
        DateTimeOffset? nextAttemptOnUtc = null)
    {
        var now = occurredOnUtc ?? DateTimeOffset.UtcNow;
        var checkout = TestCheckouts.Draft(userName) with
        {
            TotalPrice = 100,
            Items = [new BasketCheckoutLine(Guid.NewGuid(), "iPhone X", 1, 100)]
        };
        var message = BasketOutboxMessage.For(checkout, userName, now);
        message.NextAttemptOnUtc = nextAttemptOnUtc ?? now;

        await using var session = fixture.Store.LightweightSession();
        session.Store(message);
        await session.SaveChangesAsync();
        return message;
    }

    private async Task MakeDueAsync(Guid id)
    {
        await using var session = fixture.Store.LightweightSession();
        var message = await session.LoadAsync<BasketOutboxMessage>(id);
        message!.NextAttemptOnUtc = DateTimeOffset.UtcNow.AddSeconds(-1);
        session.Store(message);
        await session.SaveChangesAsync();
    }

    private async Task<IReadOnlyList<BasketOutboxMessage>> PendingAsync()
    {
        await using var session = fixture.Store.QuerySession();
        return await session.Query<BasketOutboxMessage>().ToListAsync();
    }

    private sealed class RecordingPublisher : IIntegrationEventPublisher
    {
        public List<IntegrationEventEnvelope> Published { get; } = [];
        public Exception? FailWith { get; set; }
        public Guid? FailFor { get; init; }

        public Task PublishAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
        {
            if (FailWith is not null)
            {
                throw FailWith;
            }

            if (FailFor == envelope.Id)
            {
                throw new InvalidOperationException("this message is broken (simulated)");
            }

            Published.Add(envelope);
            return Task.CompletedTask;
        }
    }
}
