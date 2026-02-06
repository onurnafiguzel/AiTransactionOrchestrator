using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IpColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "customer_ip",
                table: "transactions",
                type: "character varying(45)",
                maxLength: 45,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_customer_ip",
                table: "transactions",
                column: "customer_ip");

            migrationBuilder.CreateIndex(
                name: "idx_transactions_customer_ip_created_at",
                table: "transactions",
                columns: new[] { "customer_ip", "created_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_transactions_customer_ip",
                table: "transactions");

            migrationBuilder.DropIndex(
                name: "idx_transactions_customer_ip_created_at",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "customer_ip",
                table: "transactions");
        }
    }
}
