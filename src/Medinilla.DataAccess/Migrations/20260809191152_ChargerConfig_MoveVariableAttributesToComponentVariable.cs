using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ChargerConfig_MoveVariableAttributesToComponentVariable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataType",
                table: "core_charger_components");

            migrationBuilder.DropColumn(
                name: "MaxLimit",
                table: "core_charger_components");

            migrationBuilder.DropColumn(
                name: "MinLimit",
                table: "core_charger_components");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "core_charger_components");

            migrationBuilder.DropColumn(
                name: "ValuesList",
                table: "core_charger_components");

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "core_component_variables",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxLimit",
                table: "core_component_variables",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinLimit",
                table: "core_component_variables",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "core_component_variables",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuesList",
                table: "core_component_variables",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataType",
                table: "core_component_variables");

            migrationBuilder.DropColumn(
                name: "MaxLimit",
                table: "core_component_variables");

            migrationBuilder.DropColumn(
                name: "MinLimit",
                table: "core_component_variables");

            migrationBuilder.DropColumn(
                name: "Unit",
                table: "core_component_variables");

            migrationBuilder.DropColumn(
                name: "ValuesList",
                table: "core_component_variables");

            migrationBuilder.AddColumn<string>(
                name: "DataType",
                table: "core_charger_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxLimit",
                table: "core_charger_components",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinLimit",
                table: "core_charger_components",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Unit",
                table: "core_charger_components",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValuesList",
                table: "core_charger_components",
                type: "text",
                nullable: true);
        }
    }
}
