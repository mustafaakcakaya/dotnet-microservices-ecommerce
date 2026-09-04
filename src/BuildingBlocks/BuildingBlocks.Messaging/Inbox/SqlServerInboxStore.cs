using System.Data;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.Messaging.Inbox;

/// <summary>
/// SQL Server backed inbox. Claim and completion are two statements, which
/// leaves a narrow window: if a consumer crashes after doing its work but before
/// <see cref="CompleteAsync"/>, the work runs again. Handlers should therefore
/// either be naturally idempotent or write their business change and the
/// completion in one transaction.
/// </summary>
public sealed class SqlServerInboxStore(string connectionString) : IInboxStore
{
    private const int UniqueIndexViolation = 2601;
    private const int PrimaryKeyViolation = 2627;

    public async Task<bool> TryBeginAsync(Guid messageId, string consumerName, CancellationToken cancellationToken)
    {
        const string insertSql = """
            INSERT INTO dbo.InboxMessages (MessageId, ConsumerName, ReceivedOnUtc)
            VALUES (@MessageId, @ConsumerName, SYSUTCDATETIME());
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        try
        {
            await using var insert = CreateCommand(insertSql, connection, messageId, consumerName);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            return true;
        }
        catch (SqlException exception) when (exception.Number is UniqueIndexViolation or PrimaryKeyViolation)
        {
            const string stateSql = """
                SELECT ProcessedOnUtc
                FROM dbo.InboxMessages
                WHERE MessageId = @MessageId AND ConsumerName = @ConsumerName;
                """;

            await using var state = CreateCommand(stateSql, connection, messageId, consumerName);
            var processedOnUtc = await state.ExecuteScalarAsync(cancellationToken);

            // Completed before -> duplicate, skip. Claimed but unfinished -> let it run again.
            return processedOnUtc is null or DBNull;
        }
    }

    public async Task CompleteAsync(Guid messageId, string consumerName, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.InboxMessages
            SET ProcessedOnUtc = SYSUTCDATETIME()
            WHERE MessageId = @MessageId AND ConsumerName = @ConsumerName;
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = CreateCommand(sql, connection, messageId, consumerName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SqlCommand CreateCommand(
        string sql, SqlConnection connection, Guid messageId, string consumerName)
    {
        var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@MessageId", SqlDbType.UniqueIdentifier).Value = messageId;
        command.Parameters.Add("@ConsumerName", SqlDbType.NVarChar, 200).Value = consumerName;
        return command;
    }
}
