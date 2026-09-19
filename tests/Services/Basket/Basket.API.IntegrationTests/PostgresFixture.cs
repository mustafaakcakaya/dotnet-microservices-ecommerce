using Basket.API.Data;
using Marten;
using Testcontainers.PostgreSql;

namespace Basket.API.IntegrationTests;

/// <summary>
/// A disposable PostgreSQL with the same Marten schema the service uses, so the
/// checkout transaction is exercised against a real database rather than a fake.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .Build();

    public IDocumentStore Store { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Store = DocumentStore.For(options =>
            BasketStoreConfiguration.Configure(options, _container.GetConnectionString()));

        await Store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
    }

    public async Task DisposeAsync()
    {
        Store?.Dispose();
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(PostgresCollection))]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>;
