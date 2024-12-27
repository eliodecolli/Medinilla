using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_auth_details_charging_station_ChargingStationId",
                table: "core_auth_details");

            migrationBuilder.DropForeignKey(
                name: "FK_core_id_token_charging_station_ChargingStationId",
                table: "core_id_token");

            migrationBuilder.DropForeignKey(
                name: "FK_core_id_token_charging_station_ChargingStationId1",
                table: "core_id_token");

            migrationBuilder.DropForeignKey(
                name: "FK_evse_connector_charging_station_ChargingStationId",
                table: "evse_connector");

            migrationBuilder.DropForeignKey(
                name: "FK_tariff_charging_station_ChargingStationId",
                table: "tariff");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_event_charging_station_ChargingStationId",
                table: "transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId",
                table: "transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId1",
                table: "transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_snapshot_charging_station_ChargingStationId",
                table: "transactions_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId",
                table: "transactions_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId1",
                table: "transactions_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_snapshot_evse_connector_EvseConnectorId",
                table: "transactions_snapshot");

            migrationBuilder.DropPrimaryKey(
                name: "PK_transactions_snapshot",
                table: "transactions_snapshot");

            migrationBuilder.DropPrimaryKey(
                name: "PK_transactions_event",
                table: "transactions_event");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tariff",
                table: "tariff");

            migrationBuilder.DropPrimaryKey(
                name: "PK_evse_connector",
                table: "evse_connector");

            migrationBuilder.DropPrimaryKey(
                name: "PK_charging_station",
                table: "charging_station");

            migrationBuilder.RenameTable(
                name: "transactions_snapshot",
                newName: "core_transactions_snapshot");

            migrationBuilder.RenameTable(
                name: "transactions_event",
                newName: "core_transactions_event");

            migrationBuilder.RenameTable(
                name: "tariff",
                newName: "core_tariff");

            migrationBuilder.RenameTable(
                name: "evse_connector",
                newName: "core_evse_connector");

            migrationBuilder.RenameTable(
                name: "charging_station",
                newName: "core_charging_station");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_snapshot_TransactionId",
                table: "core_transactions_snapshot",
                newName: "IX_core_transactions_snapshot_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_snapshot_IdTokenId1",
                table: "core_transactions_snapshot",
                newName: "IX_core_transactions_snapshot_IdTokenId1");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_snapshot_IdTokenId",
                table: "core_transactions_snapshot",
                newName: "IX_core_transactions_snapshot_IdTokenId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_snapshot_EvseConnectorId",
                table: "core_transactions_snapshot",
                newName: "IX_core_transactions_snapshot_EvseConnectorId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_snapshot_ChargingStationId_TransactionId",
                table: "core_transactions_snapshot",
                newName: "IX_core_transactions_snapshot_ChargingStationId_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_event_TransactionId",
                table: "core_transactions_event",
                newName: "IX_core_transactions_event_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_event_SeqNo",
                table: "core_transactions_event",
                newName: "IX_core_transactions_event_SeqNo");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_event_IdTokenId1",
                table: "core_transactions_event",
                newName: "IX_core_transactions_event_IdTokenId1");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_event_IdTokenId",
                table: "core_transactions_event",
                newName: "IX_core_transactions_event_IdTokenId");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_event_EventType",
                table: "core_transactions_event",
                newName: "IX_core_transactions_event_EventType");

            migrationBuilder.RenameIndex(
                name: "IX_transactions_event_ChargingStationId_TransactionId",
                table: "core_transactions_event",
                newName: "IX_core_transactions_event_ChargingStationId_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_tariff_ChargingStationId",
                table: "core_tariff",
                newName: "IX_core_tariff_ChargingStationId");

            migrationBuilder.RenameIndex(
                name: "IX_evse_connector_ChargingStationId",
                table: "core_evse_connector",
                newName: "IX_core_evse_connector_ChargingStationId");

            migrationBuilder.RenameIndex(
                name: "IX_charging_station_ClientIdentifier",
                table: "core_charging_station",
                newName: "IX_core_charging_station_ClientIdentifier");

            migrationBuilder.AlterColumn<JsonDocument>(
                name: "AuthBlob",
                table: "core_auth_details",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(JsonDocument),
                oldType: "json");

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "core_charging_station",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddPrimaryKey(
                name: "PK_core_transactions_snapshot",
                table: "core_transactions_snapshot",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_core_transactions_event",
                table: "core_transactions_event",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_core_tariff",
                table: "core_tariff",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_core_evse_connector",
                table: "core_evse_connector",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_core_charging_station",
                table: "core_charging_station",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "core_account",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_account", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_charging_station_AccountId",
                table: "core_charging_station",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_core_auth_details_core_charging_station_ChargingStationId",
                table: "core_auth_details",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_charging_station_core_account_AccountId",
                table: "core_charging_station",
                column: "AccountId",
                principalTable: "core_account",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_evse_connector_core_charging_station_ChargingStationId",
                table: "core_evse_connector",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_id_token_core_charging_station_ChargingStationId",
                table: "core_id_token",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_id_token_core_charging_station_ChargingStationId1",
                table: "core_id_token",
                column: "ChargingStationId1",
                principalTable: "core_charging_station",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_tariff_core_charging_station_ChargingStationId",
                table: "core_tariff",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_event_core_charging_station_ChargingStati~",
                table: "core_transactions_event",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId",
                table: "core_transactions_event",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId1",
                table: "core_transactions_event",
                column: "IdTokenId1",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_snapshot_core_charging_station_ChargingSt~",
                table: "core_transactions_snapshot",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_snapshot_core_evse_connector_EvseConnecto~",
                table: "core_transactions_snapshot",
                column: "EvseConnectorId",
                principalTable: "core_evse_connector",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_snapshot_core_id_token_IdTokenId",
                table: "core_transactions_snapshot",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_snapshot_core_id_token_IdTokenId1",
                table: "core_transactions_snapshot",
                column: "IdTokenId1",
                principalTable: "core_id_token",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_auth_details_core_charging_station_ChargingStationId",
                table: "core_auth_details");

            migrationBuilder.DropForeignKey(
                name: "FK_core_charging_station_core_account_AccountId",
                table: "core_charging_station");

            migrationBuilder.DropForeignKey(
                name: "FK_core_evse_connector_core_charging_station_ChargingStationId",
                table: "core_evse_connector");

            migrationBuilder.DropForeignKey(
                name: "FK_core_id_token_core_charging_station_ChargingStationId",
                table: "core_id_token");

            migrationBuilder.DropForeignKey(
                name: "FK_core_id_token_core_charging_station_ChargingStationId1",
                table: "core_id_token");

            migrationBuilder.DropForeignKey(
                name: "FK_core_tariff_core_charging_station_ChargingStationId",
                table: "core_tariff");

            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_event_core_charging_station_ChargingStati~",
                table: "core_transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId",
                table: "core_transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId1",
                table: "core_transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_snapshot_core_charging_station_ChargingSt~",
                table: "core_transactions_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_snapshot_core_evse_connector_EvseConnecto~",
                table: "core_transactions_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_snapshot_core_id_token_IdTokenId",
                table: "core_transactions_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_snapshot_core_id_token_IdTokenId1",
                table: "core_transactions_snapshot");

            migrationBuilder.DropTable(
                name: "core_account");

            migrationBuilder.DropPrimaryKey(
                name: "PK_core_transactions_snapshot",
                table: "core_transactions_snapshot");

            migrationBuilder.DropPrimaryKey(
                name: "PK_core_transactions_event",
                table: "core_transactions_event");

            migrationBuilder.DropPrimaryKey(
                name: "PK_core_tariff",
                table: "core_tariff");

            migrationBuilder.DropPrimaryKey(
                name: "PK_core_evse_connector",
                table: "core_evse_connector");

            migrationBuilder.DropPrimaryKey(
                name: "PK_core_charging_station",
                table: "core_charging_station");

            migrationBuilder.DropIndex(
                name: "IX_core_charging_station_AccountId",
                table: "core_charging_station");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "core_charging_station");

            migrationBuilder.RenameTable(
                name: "core_transactions_snapshot",
                newName: "transactions_snapshot");

            migrationBuilder.RenameTable(
                name: "core_transactions_event",
                newName: "transactions_event");

            migrationBuilder.RenameTable(
                name: "core_tariff",
                newName: "tariff");

            migrationBuilder.RenameTable(
                name: "core_evse_connector",
                newName: "evse_connector");

            migrationBuilder.RenameTable(
                name: "core_charging_station",
                newName: "charging_station");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_snapshot_TransactionId",
                table: "transactions_snapshot",
                newName: "IX_transactions_snapshot_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_snapshot_IdTokenId1",
                table: "transactions_snapshot",
                newName: "IX_transactions_snapshot_IdTokenId1");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_snapshot_IdTokenId",
                table: "transactions_snapshot",
                newName: "IX_transactions_snapshot_IdTokenId");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_snapshot_EvseConnectorId",
                table: "transactions_snapshot",
                newName: "IX_transactions_snapshot_EvseConnectorId");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_snapshot_ChargingStationId_TransactionId",
                table: "transactions_snapshot",
                newName: "IX_transactions_snapshot_ChargingStationId_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_event_TransactionId",
                table: "transactions_event",
                newName: "IX_transactions_event_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_event_SeqNo",
                table: "transactions_event",
                newName: "IX_transactions_event_SeqNo");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_event_IdTokenId1",
                table: "transactions_event",
                newName: "IX_transactions_event_IdTokenId1");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_event_IdTokenId",
                table: "transactions_event",
                newName: "IX_transactions_event_IdTokenId");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_event_EventType",
                table: "transactions_event",
                newName: "IX_transactions_event_EventType");

            migrationBuilder.RenameIndex(
                name: "IX_core_transactions_event_ChargingStationId_TransactionId",
                table: "transactions_event",
                newName: "IX_transactions_event_ChargingStationId_TransactionId");

            migrationBuilder.RenameIndex(
                name: "IX_core_tariff_ChargingStationId",
                table: "tariff",
                newName: "IX_tariff_ChargingStationId");

            migrationBuilder.RenameIndex(
                name: "IX_core_evse_connector_ChargingStationId",
                table: "evse_connector",
                newName: "IX_evse_connector_ChargingStationId");

            migrationBuilder.RenameIndex(
                name: "IX_core_charging_station_ClientIdentifier",
                table: "charging_station",
                newName: "IX_charging_station_ClientIdentifier");

            migrationBuilder.AlterColumn<JsonDocument>(
                name: "AuthBlob",
                table: "core_auth_details",
                type: "json",
                nullable: false,
                oldClrType: typeof(JsonDocument),
                oldType: "jsonb");

            migrationBuilder.AddPrimaryKey(
                name: "PK_transactions_snapshot",
                table: "transactions_snapshot",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_transactions_event",
                table: "transactions_event",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_tariff",
                table: "tariff",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_evse_connector",
                table: "evse_connector",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_charging_station",
                table: "charging_station",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_core_auth_details_charging_station_ChargingStationId",
                table: "core_auth_details",
                column: "ChargingStationId",
                principalTable: "charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_id_token_charging_station_ChargingStationId",
                table: "core_id_token",
                column: "ChargingStationId",
                principalTable: "charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_core_id_token_charging_station_ChargingStationId1",
                table: "core_id_token",
                column: "ChargingStationId1",
                principalTable: "charging_station",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_evse_connector_charging_station_ChargingStationId",
                table: "evse_connector",
                column: "ChargingStationId",
                principalTable: "charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_tariff_charging_station_ChargingStationId",
                table: "tariff",
                column: "ChargingStationId",
                principalTable: "charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_event_charging_station_ChargingStationId",
                table: "transactions_event",
                column: "ChargingStationId",
                principalTable: "charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId",
                table: "transactions_event",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId1",
                table: "transactions_event",
                column: "IdTokenId1",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_snapshot_charging_station_ChargingStationId",
                table: "transactions_snapshot",
                column: "ChargingStationId",
                principalTable: "charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId",
                table: "transactions_snapshot",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId1",
                table: "transactions_snapshot",
                column: "IdTokenId1",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_snapshot_evse_connector_EvseConnectorId",
                table: "transactions_snapshot",
                column: "EvseConnectorId",
                principalTable: "evse_connector",
                principalColumn: "Id");
        }
    }
}
