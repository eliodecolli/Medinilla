using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTx_UseRegisterAndPhases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalConsuption",
                table: "core_transactions_event",
                newName: "RegisterValue");

            migrationBuilder.AddColumn<decimal>(
                name: "PhaseOneValue",
                table: "core_transactions_event",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhaseThreeValue",
                table: "core_transactions_event",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PhaseTwoValue",
                table: "core_transactions_event",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhaseOneValue",
                table: "core_transactions_event");

            migrationBuilder.DropColumn(
                name: "PhaseThreeValue",
                table: "core_transactions_event");

            migrationBuilder.DropColumn(
                name: "PhaseTwoValue",
                table: "core_transactions_event");

            migrationBuilder.RenameColumn(
                name: "RegisterValue",
                table: "core_transactions_event",
                newName: "TotalConsuption");
        }
    }
}
