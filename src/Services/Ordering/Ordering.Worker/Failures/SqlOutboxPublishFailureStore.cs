using System.Data;
using Microsoft.Data.SqlClient;
using Ordering.Worker.Data;

namespace Ordering.Worker.Failures;

public sealed class SqlOutboxPublishFailureStore(SqlConnectionFactory connectionFactory) : IOutboxPublishFailureStore
{
    public async Task<OutboxPublishFailure?> GetAsync(Guid messageId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Attempts, PoisonedOnUtc
            FROM dbo.OutboxPublishFailures
            WHERE OutboxMessageId = @Id;
            """;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = messageId;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new OutboxPublishFailure(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : DateTime.SpecifyKind(reader.GetDateTime(1), DateTimeKind.Utc));
    }

    public async Task<int> RecordFailureAsync(Guid messageId, string error, CancellationToken cancellationToken)
    {
        const string sql = """
            MERGE dbo.OutboxPublishFailures WITH (HOLDLOCK) AS target
            USING (SELECT @Id AS OutboxMessageId) AS source
                ON target.OutboxMessageId = source.OutboxMessageId
            WHEN MATCHED THEN
                UPDATE SET Attempts = target.Attempts + 1, LastError = @Error, LastAttemptOnUtc = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (OutboxMessageId, Attempts, LastError, LastAttemptOnUtc)
                VALUES (@Id, 1, @Error, SYSUTCDATETIME())
            OUTPUT inserted.Attempts;
            """;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = messageId;
        command.Parameters.Add("@Error", SqlDbType.NVarChar, 2000).Value =
            error.Length > 2000 ? error[..2000] : error;

        var attempts = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(attempts);
    }

    public async Task MarkPoisonedAsync(Guid messageId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.OutboxPublishFailures
            SET PoisonedOnUtc = SYSUTCDATETIME()
            WHERE OutboxMessageId = @Id AND PoisonedOnUtc IS NULL;
            """;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = messageId;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
