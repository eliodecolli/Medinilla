using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.Core.Service.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "core_auth_user",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ActiveCredit = table.Column<decimal>(type: "numeric", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_auth_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_charging_station",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthDetailsId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientIdentifier = table.Column<string>(type: "text", nullable: false),
                    Model = table.Column<string>(type: "text", nullable: false),
                    Vendor = table.Column<string>(type: "text", nullable: false),
                    LatestBootNotificationReason = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: true),
                    Alias = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_charging_station", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_charging_station_core_account_AccountId",
                        column: x => x.AccountId,
                        principalTable: "core_account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_auth_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthBlob = table.Column<JsonDocument>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_auth_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_auth_details_core_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_evse_connector",
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
                    table.PrimaryKey("PK_core_evse_connector", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_evse_connector_core_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_id_token",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorizationUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    IdType = table.Column<string>(type: "text", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsUnderTx = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_id_token", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_id_token_core_auth_user_AuthorizationUserId",
                        column: x => x.AuthorizationUserId,
                        principalTable: "core_auth_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_id_token_core_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_tariff",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitName = table.Column<string>(type: "text", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_tariff", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_tariff_core_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_transactions_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    SeqNo = table.Column<int>(type: "integer", nullable: false),
                    EVSEId = table.Column<int>(type: "integer", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Offline = table.Column<bool>(type: "boolean", nullable: true),
                    TotalConsuption = table.Column<decimal>(type: "numeric", nullable: false),
                    ConsumptionType = table.Column<string>(type: "text", nullable: true),
                    UnitName = table.Column<string>(type: "text", nullable: false),
                    TriggerReason = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false)
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
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "core_transactions_snapshot",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    IdTokenId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    StartReason = table.Column<string>(type: "text", nullable: false),
                    EndReason = table.Column<string>(type: "text", nullable: false),
                    TotalMeteredValue = table.Column<decimal>(type: "numeric", nullable: false),
                    TotalCost = table.Column<decimal>(type: "numeric", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EvseConnectorId = table.Column<Guid>(type: "uuid", nullable: true)
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
                name: "IX_core_auth_details_ChargingStationId",
                table: "core_auth_details",
                column: "ChargingStationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_charging_station_AccountId",
                table: "core_charging_station",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_core_charging_station_ClientIdentifier",
                table: "core_charging_station",
                column: "ClientIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_core_evse_connector_ChargingStationId",
                table: "core_evse_connector",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_core_id_token_AuthorizationUserId",
                table: "core_id_token",
                column: "AuthorizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_core_id_token_ChargingStationId_Token",
                table: "core_id_token",
                columns: new[] { "ChargingStationId", "Token" });

            migrationBuilder.CreateIndex(
                name: "IX_core_tariff_ChargingStationId",
                table: "core_tariff",
                column: "ChargingStationId");

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
                column: "TransactionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_auth_details");

            migrationBuilder.DropTable(
                name: "core_tariff");

            migrationBuilder.DropTable(
                name: "core_transactions_event");

            migrationBuilder.DropTable(
                name: "core_transactions_snapshot");

            migrationBuilder.DropTable(
                name: "core_evse_connector");

            migrationBuilder.DropTable(
                name: "core_id_token");

            migrationBuilder.DropTable(
                name: "core_auth_user");

            migrationBuilder.DropTable(
                name: "core_charging_station");

            migrationBuilder.DropTable(
                name: "core_account");
        }
    }
}
