namespace Payment.API.Data;

/// <summary>
/// The Marten schema in one place, so the service and its tests cannot drift apart.
/// </summary>
public static class PaymentStoreConfiguration
{
    public static void Configure(StoreOptions options, string connectionString)
    {
        options.Connection(connectionString);

        // StoredCard keeps its setters private so it can only be created valid.
        options.UseDefaultSerialization(nonPublicMembersStorage: NonPublicMembersStorage.All);

        options.Schema.For<StoredCard>().Identity(x => x.Id);
    }
}
