using Basket.API.Outbox;

namespace Basket.API.Data;

/// <summary>
/// The Marten schema in one place, so the service and its integration tests
/// cannot drift apart.
/// </summary>
public static class BasketStoreConfiguration
{
    public static void Configure(StoreOptions options, string connectionString)
    {
        options.Connection(connectionString);

        options.Schema.For<ShoppingCart>().Identity(x => x.UserName);

        // The relay polls for due, non-poisoned messages in arrival order.
        options.Schema.For<BasketOutboxMessage>()
            .Index(x => x.NextAttemptOnUtc)
            .Index(x => x.OccurredOnUtc);
    }
}
