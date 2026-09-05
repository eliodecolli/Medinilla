using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AuthorizationUser_AddChargingStationRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_core_auth_user_ChargingStationId",
                table: "core_auth_user",
                column: "ChargingStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_core_auth_user_core_charging_station_ChargingStationId",
                table: "core_auth_user",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_auth_user_core_charging_station_ChargingStationId",
                table: "core_auth_user");

            migrationBuilder.DropIndex(
                name: "IX_core_auth_user_ChargingStationId",
                table: "core_auth_user");
        }
    }
}
