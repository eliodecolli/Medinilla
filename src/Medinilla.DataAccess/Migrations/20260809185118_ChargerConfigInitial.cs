using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ChargerConfigInitial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "core_charger_components",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvseConnectorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientIdentifier = table.Column<string>(type: "text", nullable: false),
                    ComponentName = table.Column<string>(type: "text", nullable: false),
                    ComponentInstance = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    DataType = table.Column<string>(type: "text", nullable: true),
                    MinLimit = table.Column<decimal>(type: "numeric", nullable: true),
                    MaxLimit = table.Column<decimal>(type: "numeric", nullable: true),
                    ValuesList = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_charger_components", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_charger_components_core_charging_station_ChargingStati~",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_charger_components_core_evse_connector_EvseConnectorId",
                        column: x => x.EvseConnectorId,
                        principalTable: "core_evse_connector",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "core_report_base_statuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<long>(type: "bigint", nullable: false),
                    SeqNo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_report_base_statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "core_component_variables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChargerComponentId = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Instance = table.Column<string>(type: "text", nullable: true),
                    Value = table.Column<string>(type: "text", nullable: true),
                    Constant = table.Column<bool>(type: "boolean", nullable: true),
                    AttributeType = table.Column<string>(type: "text", nullable: false),
                    Mutability = table.Column<string>(type: "text", nullable: false),
                    ChargingStationId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_core_component_variables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_core_component_variables_core_charger_components_ChargerCom~",
                        column: x => x.ChargerComponentId,
                        principalTable: "core_charger_components",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_core_component_variables_core_charging_station_ChargingStat~",
                        column: x => x.ChargingStationId,
                        principalTable: "core_charging_station",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_core_charger_components_ChargingStationId",
                table: "core_charger_components",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_core_charger_components_ClientIdentifier",
                table: "core_charger_components",
                column: "ClientIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_core_charger_components_ComponentName_ComponentInstance",
                table: "core_charger_components",
                columns: new[] { "ComponentName", "ComponentInstance" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_core_charger_components_EvseConnectorId",
                table: "core_charger_components",
                column: "EvseConnectorId");

            migrationBuilder.CreateIndex(
                name: "IX_core_component_variables_ChargerComponentId",
                table: "core_component_variables",
                column: "ChargerComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_core_component_variables_ChargingStationId",
                table: "core_component_variables",
                column: "ChargingStationId");

            migrationBuilder.CreateIndex(
                name: "IX_core_component_variables_Name_AttributeType",
                table: "core_component_variables",
                columns: new[] { "Name", "AttributeType" });

            migrationBuilder.CreateIndex(
                name: "IX_core_component_variables_Name_Instance",
                table: "core_component_variables",
                columns: new[] { "Name", "Instance" });

            migrationBuilder.CreateIndex(
                name: "IX_core_report_base_statuses_RequestId",
                table: "core_report_base_statuses",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_core_report_base_statuses_RequestId_SeqNo",
                table: "core_report_base_statuses",
                columns: new[] { "RequestId", "SeqNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "core_component_variables");

            migrationBuilder.DropTable(
                name: "core_report_base_statuses");

            migrationBuilder.DropTable(
                name: "core_charger_components");
        }
    }
}
