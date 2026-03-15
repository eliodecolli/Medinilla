using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.Core.Service.Migrations
{
    /// <inheritdoc />
    public partial class UpdateChargingStation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Booted",
                table: "core_charging_station",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Booted",
                table: "core_charging_station");
        }
    }
}
