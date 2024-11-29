using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "charging_station",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientIdentifier = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Vendor = table.Column<string>(type: "text", nullable: false),
                    LatestBootNotificationReason = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_charging_station", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "evse_connector",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvseId = table.Column<int>(type: "integer", nullable: false),
                    ConnectorId = table.Column<int>(type: "integer", nullable: false),
                    ConnectorStatus = table.Column<string>(type: "text", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evse_connector", x => x.Id);
                    table.ForeignKey(
                        name: "FK_evse_connector_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tariff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitName = table.Column<string>(type: "text", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tariff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_tariff_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions_snapshot",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalMeteredValue = table.Column<decimal>(type: "numeric", nullable: false),
                    Unit = table.Column<string>(type: "text", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TokenId = table.Column<string>(type: "text", nullable: false),
                    EvseConnectorId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions_snapshot", x => x.TransactionId);
                    table.ForeignKey(
                        name: "FK_transactions_snapshot_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transactions_snapshot_evse_connector_EvseConnectorId",
                        column: x => x.EvseConnectorId,
                        principalTable: "evse_connector",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "transactions_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    SeqNo = table.Column<int>(type: "integer", nullable: false),
                    EVSEId = table.Column<int>(type: "integer", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdToken = table.Column<string>(type: "text", nullable: true),
                    Offline = table.Column<bool>(type: "boolean", nullable: true),
                    MeteredValue = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitName = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions_event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_transactions_event_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_transactions_event_transactions_snapshot_TransactionId",
                        column: x => x.TransactionId,
                        principalTable: "transactions_snapshot",
                        principalColumn: "TransactionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_charging_station_ClientIdentifier",
                table: "charging_station",
                column: "ClientIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_evse_connector_ChargingStationId",
                table: "evse_connector",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_tariff_ChargingStationId",
                table: "tariff",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_ChargingStationId",
                table: "transactions_event",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_TransactionId",
                table: "transactions_event",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_snapshot_ChargingStationId",
                table: "transactions_snapshot",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_snapshot_EvseConnectorId",
                table: "transactions_snapshot",
                column: "EvseConnectorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tariff");

            migrationBuilder.DropTable(
                name: "transactions_event");

            migrationBuilder.DropTable(
                name: "transactions_snapshot");

            migrationBuilder.DropTable(
                name: "evse_connector");

            migrationBuilder.DropTable(
                name: "charging_station");
        }
    }
}
