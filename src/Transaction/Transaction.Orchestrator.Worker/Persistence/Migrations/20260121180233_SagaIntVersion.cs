using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction.Orchestrator.Worker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SagaIntVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "version",
                table: "transaction_orchestrations",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "version",
                table: "transaction_orchestrations");
        }
    }
}
