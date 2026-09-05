using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class TransactionEvent_RequireIdToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId",
                table: "core_transactions_event");

            migrationBuilder.AlterColumn<Guid>(
                name: "IdTokenId",
                table: "core_transactions_event",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId",
                table: "core_transactions_event",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId",
                table: "core_transactions_event");

            migrationBuilder.AlterColumn<Guid>(
                name: "IdTokenId",
                table: "core_transactions_event",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddForeignKey(
                name: "FK_core_transactions_event_core_id_token_IdTokenId",
                table: "core_transactions_event",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id");
        }
    }
}
