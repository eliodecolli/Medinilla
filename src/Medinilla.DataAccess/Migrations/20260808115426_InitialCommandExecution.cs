using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCommandExecution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_command_executions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChargingStationClientIdentifier = table.Column<string>(type: "text", nullable: false),
                    ActionName = table.Column<string>(type: "text", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Completed = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_command_executions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_command_executions_core_charging_station_ChargingStati~",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_command_executions_ChargingStationId",
                table: "core_command_executions",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_core_command_executions_MessageId",
                table: "core_command_executions",
                column: "MessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_command_executions");
        }
    }
}
