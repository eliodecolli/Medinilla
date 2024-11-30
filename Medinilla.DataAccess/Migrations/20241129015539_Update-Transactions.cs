using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_event_transactions_snapshot_TransactionId",
                table: "transactions_event");

            migrationBuilder.DropPrimaryKey(
                name: "PK_transactions_snapshot",
                table: "transactions_snapshot");

            migrationBuilder.AddColumn<Guid>(
                name: "Id",
                table: "transactions_snapshot",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_transactions_snapshot",
                table: "transactions_snapshot",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_transactions_snapshot",
                table: "transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "transactions_snapshot");

            migrationBuilder.AddPrimaryKey(
                name: "PK_transactions_snapshot",
                table: "transactions_snapshot",
                column: "TransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_event_transactions_snapshot_TransactionId",
                table: "transactions_event",
                column: "TransactionId",
                principalTable: "transactions_snapshot",
                principalColumn: "TransactionId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
