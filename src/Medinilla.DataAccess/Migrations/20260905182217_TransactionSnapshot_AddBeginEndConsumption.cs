using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class TransactionSnapshot_AddBeginEndConsumption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_core_transactions_snapshot_TransactionId",
                table: "core_transactions_snapshot");

            migrationBuilder.AddColumn<decimal>(
                name: "BeginConsumption",
                table: "core_transactions_snapshot",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EndConsumption",
                table: "core_transactions_snapshot",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LatestSeqNo",
                table: "core_transactions_snapshot",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalConsumption",
                table: "core_transactions_snapshot",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_snapshot_TransactionId",
                table: "core_transactions_snapshot",
                column: "TransactionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_core_transactions_snapshot_TransactionId",
                table: "core_transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "BeginConsumption",
                table: "core_transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "EndConsumption",
                table: "core_transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "LatestSeqNo",
                table: "core_transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "TotalConsumption",
                table: "core_transactions_snapshot");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_snapshot_TransactionId",
                table: "core_transactions_snapshot",
                column: "TransactionId");
        }
    }
}
