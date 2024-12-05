using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AuthorizationInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuthDetailsId",
                table: "charging_station",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "core_auth_details",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthBlob = table.Column<JsonDocument>(type: "json", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_auth_details", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_auth_details_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "core_auth_user",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_auth_user", x => x.Id);
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
                    UnderTx = table.Column<bool>(type: "boolean", nullable: false),
                    Blocked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    ChargingStationId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_id_token", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_id_token_charging_station_ChargingStationId",
                        column: x => x.ChargingStationId,
                        principalTable: "charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_id_token_charging_station_ChargingStationId1",
                        column: x => x.ChargingStationId1,
                        principalTable: "charging_station",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_core_id_token_core_auth_user_AuthorizationUserId",
                        column: x => x.AuthorizationUserId,
                        principalTable: "core_auth_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_auth_details_ChargingStationId",
                table: "core_auth_details",
                column: "ChargingStationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_id_token_AuthorizationUserId",
                table: "core_id_token",
                column: "AuthorizationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_core_id_token_ChargingStationId1",
                table: "core_id_token",
                column: "ChargingStationId1");

            migrationBuilder.CreateIndex(
                name: "IX_core_id_token_ChargingStationId_Token",
                table: "core_id_token",
                columns: new[] { "ChargingStationId", "Token" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_auth_details");

            migrationBuilder.DropTable(
                name: "core_id_token");

            migrationBuilder.DropTable(
                name: "core_auth_user");

            migrationBuilder.DropColumn(
                name: "AuthDetailsId",
                table: "charging_station");
        }
    }
}
