using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddTransactionSampleIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChargingStationId",
                table: "core_transaction_samples",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "core_transaction_samples",
                type: "text",
                nullable: true);

            // Preserve identity for existing samples using their parent events.
            migrationBuilder.Sql("""
                UPDATE core_transaction_samples AS s
                SET "ChargingStationId" = e."ChargingStationId",
                    "TransactionId" = e."TransactionId"
                FROM core_transactions_event AS e
                WHERE s."TransactionEventId" = e."Id";
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "ChargingStationId",
                table: "core_transaction_samples",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TransactionId",
                table: "core_transaction_samples",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_transaction_samples_ChargingStationId_TransactionId",
                table: "core_transaction_samples",
                columns: new[] { "ChargingStationId", "TransactionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_core_transaction_samples_core_charging_station_ChargingStat~",
                table: "core_transaction_samples",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_transaction_samples_core_charging_station_ChargingStat~",
                table: "core_transaction_samples");

            migrationBuilder.DropIndex(
                name: "IX_core_transaction_samples_ChargingStationId_TransactionId",
                table: "core_transaction_samples");

            migrationBuilder.DropColumn(
                name: "ChargingStationId",
                table: "core_transaction_samples");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "core_transaction_samples");
        }
    }
}
