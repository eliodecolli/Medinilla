using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class DropCoreTransactionsEventAndSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_transactions_event");

            migrationBuilder.DropTable(
                name: "core_transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "IsUnderTx",
                table: "core_id_token");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsUnderTx",
                table: "core_id_token",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "core_transactions_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdTokenId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumptionType = table.Column<string>(type: "text", nullable: true),
                    EVSEId = table.Column<int>(type: "integer", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Offline = table.Column<bool>(type: "boolean", nullable: true),
                    PhaseOneValue = table.Column<decimal>(type: "numeric", nullable: true),
                    PhaseThreeValue = table.Column<decimal>(type: "numeric", nullable: true),
                    PhaseTwoValue = table.Column<decimal>(type: "numeric", nullable: true),
                    RegisterValue = table.Column<decimal>(type: "numeric", nullable: false),
                    SeqNo = table.Column<int>(type: "integer", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    TriggerReason = table.Column<string>(type: "text", nullable: false),
                    UnitName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_transactions_event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_transactions_event_core_charging_station_ChargingStati~",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_transactions_event_core_id_token_IdTokenId",
                        column: x => x.IdTokenId,
                        principalTable: "core_id_token",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_transactions_snapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvseConnectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    IdTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeginConsumption = table.Column<decimal>(type: "numeric", nullable: true),
                    EndConsumption = table.Column<decimal>(type: "numeric", nullable: true),
                    EndReason = table.Column<string>(type: "text", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LatestSeqNo = table.Column<int>(type: "integer", nullable: false),
                    StartReason = table.Column<string>(type: "text", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalConsumption = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalMeteredValue = table.Column<decimal>(type: "numeric", nullable: false),
                    TransactionId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_transactions_snapshot", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_transactions_snapshot_core_charging_station_ChargingSt~",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_transactions_snapshot_core_evse_connector_EvseConnecto~",
                        column: x => x.EvseConnectorId,
                        principalTable: "core_evse_connector",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_core_transactions_snapshot_core_id_token_IdTokenId",
                        column: x => x.IdTokenId,
                        principalTable: "core_id_token",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_ChargingStationId_TransactionId",
                table: "core_transactions_event",
                columns: new[] { "ChargingStationId", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_EventType",
                table: "core_transactions_event",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_IdTokenId",
                table: "core_transactions_event",
                column: "IdTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_SeqNo",
                table: "core_transactions_event",
                column: "SeqNo");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_TransactionId",
                table: "core_transactions_event",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_snapshot_ChargingStationId_TransactionId",
                table: "core_transactions_snapshot",
                columns: new[] { "ChargingStationId", "TransactionId" });

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_snapshot_EvseConnectorId",
                table: "core_transactions_snapshot",
                column: "EvseConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_snapshot_IdTokenId",
                table: "core_transactions_snapshot",
                column: "IdTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_snapshot_TransactionId",
                table: "core_transactions_snapshot",
                column: "TransactionId",
                unique: true);
        }
    }
}
