using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction.Orchestrator.Worker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SagaSecond : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "fraud_timeout_token_id",
                table: "transaction_orchestrations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "transaction_orchestrations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "timed_out_at_utc",
                table: "transaction_orchestrations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "fraud_timeout_token_id",
                table: "transaction_orchestrations");

            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "transaction_orchestrations");

            migrationBuilder.DropColumn(
                name: "timed_out_at_utc",
                table: "transaction_orchestrations");
        }
    }
}
