using System.Data;
using BuildingBlocks.Messaging.Events;
using Microsoft.Data.SqlClient;
using Ordering.Worker.Data;

namespace Ordering.Worker.Cdc;

/// <summary>
/// Reads OutboxMessages INSERTs from the SQL Server CDC change table
/// (capture instance <c>dbo_OutboxMessages</c>). Publishing is a separate
/// concern handled by <c>IIntegrationEventPublisher</c>.
/// </summary>
public sealed class SqlCdcOutboxReader(SqlConnectionFactory connectionFactory) : ICdcOutboxReader
{
    // Fixed identifier baked into the CDC function names below; not user input.
    public const string CaptureInstance = "dbo_OutboxMessages";

    private const string SelectColumns =
        "[__$start_lsn], [Id], [EventType], [SchemaVersion], [AggregateId], [CorrelationId], [OccurredOnUtc], [Payload]";

    public async Task<CdcBatch> ReadBatchAsync(byte[]? afterLsn, int batchSize, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

        await using var connection = connectionFactory.Create();
        await connection.OpenAsync(cancellationToken);

        var (minLsn, maxLsn, fromLsn) = await GetLsnRangeAsync(connection, afterLsn, cancellationToken);

        if (Lsn.IsZero(maxLsn))
        {
            throw new CdcNotReadyException(
                "sys.fn_cdc_get_max_lsn() returned no value; CDC does not appear to be enabled on this database.");
        }

        if (Lsn.IsZero(minLsn))
        {
            throw new CdcNotReadyException(
                $"No minimum LSN for capture instance '{CaptureInstance}'; the capture instance does not exist " +
                "or the capture job has not started (check that SQL Server Agent is running).");
        }

        if (afterLsn is not null && Lsn.Compare(fromLsn!, minLsn!) < 0)
        {
            // Changes between the checkpoint and the retention floor were cleaned up.
            throw new CdcCheckpointExpiredException(Lsn.ToHex(afterLsn), Lsn.ToHex(minLsn));
        }

        var effectiveFrom = afterLsn is null ? minLsn! : fromLsn!;
        if (Lsn.Compare(effectiveFrom, maxLsn!) > 0)
        {
            return CdcBatch.Empty;
        }

        // Read one extra row to detect whether the last LSN group got truncated by TOP.
        var rows = await QueryChangesAsync(connection, effectiveFrom, maxLsn!, batchSize + 1, cancellationToken);
        if (rows.Count == 0)
        {
            return CdcBatch.Empty;
        }

        var truncated = rows.Count > batchSize;
        if (truncated)
        {
            var lastLsn = rows[^1].Lsn;
            rows.RemoveAll(row => Lsn.Compare(row.Lsn, lastLsn) == 0);

            if (rows.Count == 0)
            {
                // A single source transaction is larger than the batch size:
                // fetch that LSN group in full so it can be processed atomically.
                rows = await QueryChangesAsync(connection, lastLsn, lastLsn, top: null, cancellationToken);
            }
        }

        return new CdcBatch(GroupByLsn(rows));
    }

    private static async Task<(byte[]? MinLsn, byte[]? MaxLsn, byte[]? FromLsn)> GetLsnRangeAsync(
        SqlConnection connection, byte[]? afterLsn, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                sys.fn_cdc_get_min_lsn(@CaptureInstance) AS MinLsn,
                sys.fn_cdc_get_max_lsn() AS MaxLsn,
                sys.fn_cdc_increment_lsn(@AfterLsn) AS FromLsn;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@CaptureInstance", SqlDbType.NVarChar, 128).Value = CaptureInstance;
        command.Parameters.Add("@AfterLsn", SqlDbType.Binary, Lsn.Length).Value =
            (object?)afterLsn ?? DBNull.Value;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);

        return (
            reader.IsDBNull(0) ? null : (byte[])reader.GetValue(0),
            reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1),
            reader.IsDBNull(2) ? null : (byte[])reader.GetValue(2));
    }

    private static async Task<List<ChangeRow>> QueryChangesAsync(
        SqlConnection connection, byte[] fromLsn, byte[] toLsn, int? top, CancellationToken cancellationToken)
    {
        var topClause = top.HasValue ? "TOP (@Top) " : string.Empty;
        var sql = $"""
            SELECT {topClause}{SelectColumns}
            FROM cdc.fn_cdc_get_all_changes_{CaptureInstance}(@FromLsn, @ToLsn, N'all')
            WHERE [__$operation] = 2
            ORDER BY [__$start_lsn], [__$seqval];
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@FromLsn", SqlDbType.Binary, Lsn.Length).Value = fromLsn;
        command.Parameters.Add("@ToLsn", SqlDbType.Binary, Lsn.Length).Value = toLsn;
        if (top.HasValue)
        {
            command.Parameters.Add("@Top", SqlDbType.Int).Value = top.Value;
        }

        var rows = new List<ChangeRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new ChangeRow(
                (byte[])reader.GetValue(0),
                new IntegrationEventEnvelope(
                    reader.GetGuid(1),
                    reader.GetString(2),
                    reader.GetInt32(3),
                    reader.GetString(4),
                    reader.IsDBNull(5) ? null : reader.GetString(5),
                    DateTime.SpecifyKind(reader.GetDateTime(6), DateTimeKind.Utc),
                    reader.GetString(7))));
        }

        return rows;
    }

    private static List<CdcLsnGroup> GroupByLsn(List<ChangeRow> rows)
    {
        var groups = new List<CdcLsnGroup>();
        var currentLsn = default(byte[]);
        var currentMessages = new List<IntegrationEventEnvelope>();

        foreach (var row in rows)
        {
            if (currentLsn is null || Lsn.Compare(row.Lsn, currentLsn) != 0)
            {
                if (currentLsn is not null)
                {
                    groups.Add(new CdcLsnGroup(currentLsn, currentMessages));
                }

                currentLsn = row.Lsn;
                currentMessages = [];
            }

            currentMessages.Add(row.Message);
        }

        if (currentLsn is not null)
        {
            groups.Add(new CdcLsnGroup(currentLsn, currentMessages));
        }

        return groups;
    }

    private sealed record ChangeRow(byte[] Lsn, IntegrationEventEnvelope Message);
}
