namespace Ordering.IntegrationTests;

/// <summary>
/// All integration tests share one SQL Server container. Collections run
/// sequentially in xunit, so tests don't race on the CDC checkpoint state.
/// </summary>
[CollectionDefinition(nameof(SqlServerCdcCollection))]
public sealed class SqlServerCdcCollection : ICollectionFixture<SqlServerCdcFixture>;
