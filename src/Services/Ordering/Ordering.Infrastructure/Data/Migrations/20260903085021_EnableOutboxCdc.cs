using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Enables SQL Server CDC on OrderDb and creates the capture instance for
    /// dbo.OutboxMessages — the only table that is tracked.
    ///
    /// Every statement runs with <c>suppressTransaction: true</c>: EF wraps
    /// migrations in a transaction by default, but sys.sp_cdc_enable_db and
    /// sys.sp_cdc_enable_table refuse to run inside one ("cannot be executed
    /// within a transaction"). Suppressing it is what allows the CDC setup to be
    /// part of the migration instead of a separate deployment script.
    ///
    /// Requirements: SQL Server Standard/Enterprise/Developer edition (not
    /// Express) and a login with db_owner rights. SQL Server Agent must be
    /// running for the capture job to actually copy changes.
    /// </summary>
    public partial class EnableOutboxCdc : Migration
    {
        private const string CaptureInstance = "dbo_OutboxMessages";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF (SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME()) = 0
                    EXEC sys.sp_cdc_enable_db;
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                $"""
                IF NOT EXISTS (SELECT 1 FROM cdc.change_tables WHERE capture_instance = N'{CaptureInstance}')
                    EXEC sys.sp_cdc_enable_table
                        @source_schema = N'dbo',
                        @source_name = N'OutboxMessages',
                        @capture_instance = N'{CaptureInstance}',
                        @role_name = NULL,
                        @supports_net_changes = 0;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                $"""
                IF EXISTS (SELECT 1 FROM cdc.change_tables WHERE capture_instance = N'{CaptureInstance}')
                    EXEC sys.sp_cdc_disable_table
                        @source_schema = N'dbo',
                        @source_name = N'OutboxMessages',
                        @capture_instance = N'{CaptureInstance}';
                """,
                suppressTransaction: true);

            migrationBuilder.Sql(
                """
                IF (SELECT is_cdc_enabled FROM sys.databases WHERE name = DB_NAME()) = 1
                    EXEC sys.sp_cdc_disable_db;
                """,
                suppressTransaction: true);
        }
    }
}
