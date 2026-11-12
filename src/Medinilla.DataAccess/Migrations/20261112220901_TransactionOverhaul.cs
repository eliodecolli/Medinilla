using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class TransactionOverhaul : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_transactions_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriggerReason = table.Column<string>(type: "text", nullable: false),
                    SeqNo = table.Column<int>(type: "integer", nullable: false),
                    Offline = table.Column<bool>(type: "boolean", nullable: true),
                    NumberOfPhasesUsed = table.Column<int>(type: "integer", nullable: true),
                    CableMaxCurrent = table.Column<int>(type: "integer", nullable: true),
                    ReservationId = table.Column<int>(type: "integer", nullable: true),
                    ChargingState = table.Column<string>(type: "text", nullable: true),
                    TimeSpentCharging = table.Column<int>(type: "integer", nullable: true),
                    StoppedReason = table.Column<string>(type: "text", nullable: true),
                    RemoteStartId = table.Column<int>(type: "integer", nullable: true),
                    EvseId = table.Column<int>(type: "integer", nullable: true),
                    ConnectorId = table.Column<int>(type: "integer", nullable: true),
                    IdTokenId = table.Column<Guid>(type: "uuid", nullable: true)
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
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "core_transaction_samples",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Measurand = table.Column<string>(type: "text", nullable: false),
                    Location = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric", nullable: false),
                    TransactionEventId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_transaction_samples", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_transaction_samples_core_transactions_event_Transactio~",
                        column: x => x.TransactionEventId,
                        principalTable: "core_transactions_event",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_transaction_samples_TransactionEventId",
                table: "core_transaction_samples",
                column: "TransactionEventId");

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_ChargingStationId_TransactionId_Seq~",
                table: "core_transactions_event",
                columns: new[] { "ChargingStationId", "TransactionId", "SeqNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_transactions_event_IdTokenId",
                table: "core_transactions_event",
                column: "IdTokenId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_transaction_samples");

            migrationBuilder.DropTable(
                name: "core_transactions_event");
        }
    }
}
