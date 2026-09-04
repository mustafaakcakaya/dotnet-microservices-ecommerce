using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Ordering.Worker.Cdc;
using Ordering.Worker.Data;

namespace Ordering.Worker.Checkpoints;

public sealed class SqlOutboxCheckpointStore(
    SqlConnectionFactory connectionFactory,
    IOptions<OutboxCdcOptions> options) : IOutboxCheckpointStore
{
    public async Task<byte[]?> GetAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT LastProcessedLsn
            FROM dbo.OutboxCdcCheckpoints
            WHERE ConsumerName = @ConsumerName;
            """;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ConsumerName", SqlDbType.NVarChar, 100).Value = options.Value.ConsumerName;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is byte[] lsn ? lsn : null;
    }

    public async Task SaveAsync(byte[] lsn, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE dbo.OutboxCdcCheckpoints
            SET LastProcessedLsn = @Lsn, UpdatedOnUtc = SYSUTCDATETIME()
            WHERE ConsumerName = @ConsumerName;

            IF @@ROWCOUNT = 0
                INSERT INTO dbo.OutboxCdcCheckpoints (ConsumerName, LastProcessedLsn, UpdatedOnUtc)
                VALUES (@ConsumerName, @Lsn, SYSUTCDATETIME());
            """;

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@ConsumerName", SqlDbType.NVarChar, 100).Value = options.Value.ConsumerName;
        command.Parameters.Add("@Lsn", SqlDbType.Binary, Lsn.Length).Value = lsn;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
