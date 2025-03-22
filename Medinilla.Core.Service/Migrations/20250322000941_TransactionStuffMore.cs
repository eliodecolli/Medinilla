using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.Core.Service.Migrations
{
    /// <inheritdoc />
    public partial class TransactionStuffMore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Unit",
                table: "core_transactions_snapshot");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEvent",
                table: "core_transactions_snapshot",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "ConsumptionType",
                table: "core_transactions_event",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastEvent",
                table: "core_transactions_snapshot");

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "core_transactions_snapshot",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "ConsumptionType",
                table: "core_transactions_event",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
