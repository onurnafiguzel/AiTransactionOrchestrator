using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Transaction.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OutboxNewFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAtUtc",
                table: "outbox_messages");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "outbox_messages",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "outbox_messages",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "outbox_messages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "FailedAtUtc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "outbox_messages",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "outbox_messages",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockedUntilUtc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "outbox_messages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_LockedUntilUtc",
                table: "outbox_messages",
                column: "LockedUntilUtc");

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAtUtc_FailedAtUtc_NextAttemptAtUtc",
                table: "outbox_messages",
                columns: new[] { "PublishedAtUtc", "FailedAtUtc", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_LockedUntilUtc",
                table: "outbox_messages");

            migrationBuilder.DropIndex(
                name: "IX_outbox_messages_PublishedAtUtc_FailedAtUtc_NextAttemptAtUtc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "FailedAtUtc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "outbox_messages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "outbox_messages");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "outbox_messages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "CorrelationId",
                table: "outbox_messages",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_PublishedAtUtc",
                table: "outbox_messages",
                column: "PublishedAtUtc");
        }
    }
}
