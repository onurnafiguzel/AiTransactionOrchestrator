using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BootFields2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Explanation",
                table: "transactions",
                newName: "explanation");

            migrationBuilder.RenameColumn(
                name: "RiskScore",
                table: "transactions",
                newName: "risk_score");

            migrationBuilder.RenameColumn(
                name: "LastDecidedAtUtc",
                table: "transactions",
                newName: "last_decided_at_utc");

            migrationBuilder.RenameColumn(
                name: "DecisionReason",
                table: "transactions",
                newName: "decision_reason");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "explanation",
                table: "transactions",
                newName: "Explanation");

            migrationBuilder.RenameColumn(
                name: "risk_score",
                table: "transactions",
                newName: "RiskScore");

            migrationBuilder.RenameColumn(
                name: "last_decided_at_utc",
                table: "transactions",
                newName: "LastDecidedAtUtc");

            migrationBuilder.RenameColumn(
                name: "decision_reason",
                table: "transactions",
                newName: "DecisionReason");
        }
    }
}
