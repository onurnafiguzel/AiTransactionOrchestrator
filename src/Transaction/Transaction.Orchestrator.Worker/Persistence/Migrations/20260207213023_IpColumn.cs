using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction.Orchestrator.Worker.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IpColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerIp",
                table: "transaction_orchestrations",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerIp",
                table: "transaction_orchestrations");
        }
    }
}
