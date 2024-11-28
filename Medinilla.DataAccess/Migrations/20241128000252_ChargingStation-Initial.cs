using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ChargingStationInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "charging_station",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "charging_station");
        }
    }
}
