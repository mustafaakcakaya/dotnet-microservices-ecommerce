namespace Payment.API.Data;

/// <summary>
/// The Marten schema in one place, so the service and its tests cannot drift apart.
/// </summary>
public static class PaymentStoreConfiguration
{
    public static void Configure(StoreOptions options, string connectionString)
    {
        options.Connection(connectionString);

        options.Schema.For<StoredCard>().Identity(x => x.Id);
    }
}
