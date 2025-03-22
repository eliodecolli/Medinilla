using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.Core.Service.Migrations
{
    /// <inheritdoc />
    public partial class TransactionEvennts2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MeteredValue",
                table: "core_transactions_event",
                newName: "TotalConsuption");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "TotalConsuption",
                table: "core_transactions_event",
                newName: "MeteredValue");
        }
    }
}
