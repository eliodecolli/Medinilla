using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ChargerConfig_RemoveComponentVariableChargingStationFK : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_component_variables_core_charging_station_ChargingStat~",
                table: "core_component_variables");

            migrationBuilder.DropIndex(
                name: "IX_core_component_variables_ChargingStationId",
                table: "core_component_variables");

            migrationBuilder.DropColumn(
                name: "ChargingStationId",
                table: "core_component_variables");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ChargingStationId",
                table: "core_component_variables",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_core_component_variables_ChargingStationId",
                table: "core_component_variables",
                column: "ChargingStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_core_component_variables_core_charging_station_ChargingStat~",
                table: "core_component_variables",
                column: "ChargingStationId",
                principalTable: "core_charging_station",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
