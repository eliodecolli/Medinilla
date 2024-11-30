using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTransactionsIndices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_snapshot_ChargingStationId",
                table: "transactions_snapshot");

            migrationBuilder.DropIndex(
                name: "IX_transactions_event_ChargingStationId",
                table: "transactions_event");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_snapshot_ChargingStationId_TransactionId",
                table: "transactions_snapshot",
                columns: new[] { "ChargingStationId", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_transactions_snapshot_TransactionId",
                table: "transactions_snapshot",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_ChargingStationId_TransactionId",
                table: "transactions_event",
                columns: new[] { "ChargingStationId", "TransactionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_snapshot_ChargingStationId_TransactionId",
                table: "transactions_snapshot");

            migrationBuilder.DropIndex(
                name: "IX_transactions_snapshot_TransactionId",
                table: "transactions_snapshot");

            migrationBuilder.DropIndex(
                name: "IX_transactions_event_ChargingStationId_TransactionId",
                table: "transactions_event");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_snapshot_ChargingStationId",
                table: "transactions_snapshot",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_ChargingStationId",
                table: "transactions_event",
                column: "ChargingStationId");
        }
    }
}
