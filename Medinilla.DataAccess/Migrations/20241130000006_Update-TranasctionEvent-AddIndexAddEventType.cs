using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class UpdateTranasctionEventAddIndexAddEventType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EventType",
                table: "transactions_event",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_EventType",
                table: "transactions_event",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_SeqNo",
                table: "transactions_event",
                column: "SeqNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_transactions_event_EventType",
                table: "transactions_event");

            migrationBuilder.DropIndex(
                name: "IX_transactions_event_SeqNo",
                table: "transactions_event");

            migrationBuilder.DropColumn(
                name: "EventType",
                table: "transactions_event");
        }
    }
}
