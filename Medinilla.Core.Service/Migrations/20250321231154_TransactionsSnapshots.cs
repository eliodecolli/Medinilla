using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.Core.Service.Migrations
{
    /// <inheritdoc />
    public partial class TransactionsSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransactionSnapshotId",
                table: "core_transactions_event",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_TransactionSnapshotId",
                table: "core_transactions_event",
                column: "TransactionSnapshotId");

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_event_core_transactions_snapshot_Transact~",
                table: "core_transactions_event",
                column: "TransactionSnapshotId",
                principalTable: "core_transactions_snapshot",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_event_core_transactions_snapshot_Transact~",
                table: "core_transactions_event");

            migrationBuilder.DropIndex(
                name: "IX_core_transactions_event_TransactionSnapshotId",
                table: "core_transactions_event");

            migrationBuilder.DropColumn(
                name: "TransactionSnapshotId",
                table: "core_transactions_event");
        }
    }
}
