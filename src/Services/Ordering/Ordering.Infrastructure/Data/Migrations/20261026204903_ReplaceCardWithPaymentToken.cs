using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ordering.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Removes card details from Orders: the card now lives in the Payment
    /// service and an order keeps only its token and last four digits.
    ///
    /// One way only. Down restores the columns' shape so earlier migrations can
    /// be applied again, but the card data itself is gone and comes back empty.
    /// </summary>
    public partial class ReplaceCardWithPaymentToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Payment_CardLast4",
                table: "Orders",
                type: "nchar(4)",
                fixedLength: true,
                maxLength: 4,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Payment_PaymentToken",
                table: "Orders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            // Existing orders were placed before the Payment service existed, so no
            // token refers to their card. They keep the last four digits and get a
            // marker token that says so, instead of an empty value.
            migrationBuilder.Sql(
                """
                UPDATE dbo.Orders
                SET Payment_CardLast4 = RIGHT(REPLACE(Payment_CardNumber, ' ', ''), 4),
                    Payment_PaymentToken = CONCAT('legacy-', CONVERT(nvarchar(36), Id));
                """);

            migrationBuilder.DropColumn(
                name: "Payment_CVV",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Payment_CardName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Payment_CardNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Payment_Expiration",
                table: "Orders");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Payment_CardLast4",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Payment_PaymentToken",
                table: "Orders");

            migrationBuilder.AddColumn<string>(
                name: "Payment_CVV",
                table: "Orders",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Payment_CardName",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Payment_CardNumber",
                table: "Orders",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Payment_Expiration",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");
        }
    }
}
