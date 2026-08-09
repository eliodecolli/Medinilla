using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class ChargerConfigFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_core_charger_components_ComponentName_ComponentInstance",
                table: "core_charger_components");

            migrationBuilder.CreateIndex(
                name: "IX_core_charger_components_ComponentName_ComponentInstance",
                table: "core_charger_components",
                columns: new[] { "ComponentName", "ComponentInstance" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_core_charger_components_ComponentName_ComponentInstance",
                table: "core_charger_components");

            migrationBuilder.CreateIndex(
                name: "IX_core_charger_components_ComponentName_ComponentInstance",
                table: "core_charger_components",
                columns: new[] { "ComponentName", "ComponentInstance" },
                unique: true);
        }
    }
}
