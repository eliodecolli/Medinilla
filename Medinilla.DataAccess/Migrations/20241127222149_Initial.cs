using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "transactions_event",
                columns: table => new
                {
                    TransactionId = table.Column<string>(type: "text", nullable: false),
                    SeqNo = table.Column<int>(type: "integer", nullable: false),
                    EVSEId = table.Column<int>(type: "integer", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IdToken = table.Column<string>(type: "text", nullable: true),
                    Offline = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transactions_event", x => new { x.TransactionId, x.SeqNo });
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "transactions_event");
        }
    }
}
