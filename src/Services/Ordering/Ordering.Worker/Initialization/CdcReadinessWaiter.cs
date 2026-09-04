using Microsoft.Data.SqlClient;
using Ordering.Worker.Data;

namespace Ordering.Worker.Initialization;

public interface ICdcReadinessWaiter
{
    /// <summary>Returns once CDC is usable, or throws if cancelled.</summary>
    Task WaitUntilReadyAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Waits until the CDC capture instance for dbo.OutboxMessages exists.
/// The worker no longer creates any schema: CDC and the worker's tables are
/// created by the Ordering EF migrations (EnableOutboxCdc). The worker can still
/// start before those migrations have been applied, so it waits here instead of
/// failing, and says exactly what it is waiting for.
/// </summary>
public sealed class CdcReadinessWaiter(
    SqlConnectionFactory connectionFactory,
    ILogger<CdcReadinessWaiter> logger) : ICdcReadinessWaiter
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    private const string ReadinessSql = """
        SELECT CASE
            WHEN (SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME()) = 0 THEN 'database-not-cdc-enabled'
            WHEN NOT EXISTS (SELECT 1 FROM cdc.change_tables WHERE capture_instance = N'dbo_OutboxMessages')
                THEN 'capture-instance-missing'
            ELSE 'ready'
        END;
        """;

    public async Task WaitUntilReadyAsync(CancellationToken cancellationToken)
    {
        var lastReason = string.Empty;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = connectionFactory.Create();
                await connection.OpenAsync(cancellationToken);

                await using var command = new SqlCommand(ReadinessSql, connection);
                var state = (string)(await command.ExecuteScalarAsync(cancellationToken))!;

                if (state == "ready")
                {
                    logger.LogInformation("CDC capture instance dbo_OutboxMessages is ready");
                    return;
                }

                if (state != lastReason)
                {
                    lastReason = state;
                    logger.LogWarning(
                        "Waiting for CDC to become available ({Reason}). " +
                        "Apply the Ordering EF migrations (EnableOutboxCdc) against this database",
                        state);
                }
            }
            catch (SqlException exception)
            {
                logger.LogWarning(exception, "CDC readiness check failed; retrying");
            }

            await Task.Delay(RetryDelay, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
