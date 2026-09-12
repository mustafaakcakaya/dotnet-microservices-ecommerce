using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;
using Ordering.Infrastructure.Data;
using Ordering.Infrastructure.Data.Interceptors;
using Ordering.Worker.Data;
using Testcontainers.MsSql;

namespace Ordering.IntegrationTests;

/// <summary>
/// Starts a disposable SQL Server container with SQL Server Agent enabled
/// (required by the CDC capture job) and applies the EF migrations — which now
/// also enable CDC and create the worker/inbox tables, so the migrations
/// themselves are exercised by every integration test. The container and all
/// data created by the tests are removed when the fixture is disposed.
/// </summary>
public sealed class SqlServerCdcFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:latest")
        .WithEnvironment("MSSQL_AGENT_ENABLED", "true")
        .Build();

    private ServiceProvider _serviceProvider = default!;

    public string ConnectionString { get; private set; } = default!;
    public SqlConnectionFactory ConnectionFactory { get; private set; } = default!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = "OrderDbTests"
        }.ConnectionString;
        ConnectionFactory = new SqlConnectionFactory(ConnectionString);

        // Real MediatR pipeline so the in-process domain event handlers run too.
        _serviceProvider = new ServiceCollection()
            .AddLogging()
            .AddMediatR(config =>
                config.RegisterServicesFromAssembly(typeof(Application.DependencyInjection).Assembly))
            .BuildServiceProvider();

        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public ApplicationDbContext CreateDbContext(IFeatureManager? featureManager = null)
    {
        var mediator = _serviceProvider.GetRequiredService<IMediator>();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .AddInterceptors(
                new AuditableEntityInterceptor(),
                new DispatchDomainEventsInterceptor(mediator, featureManager))
            .Options;

        return new ApplicationDbContext(options);
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}
