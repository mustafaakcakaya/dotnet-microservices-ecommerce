using Microsoft.Data.SqlClient;

namespace Ordering.Worker.Data;

public sealed class SqlConnectionFactory(string connectionString)
{
    public string ConnectionString { get; } = connectionString;

    public SqlConnection Create() => new(ConnectionString);
}
