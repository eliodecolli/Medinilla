using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Medinilla.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class AuthorizationIdTokenTransactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IdToken",
                table: "transactions_event");

            migrationBuilder.AddColumn<Guid>(
                name: "IdTokenId",
                table: "transactions_snapshot",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdTokenId1",
                table: "transactions_snapshot",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdTokenId",
                table: "transactions_event",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "IdTokenId1",
                table: "transactions_event",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnderTx",
                table: "core_id_token",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "ActiveCredit",
                table: "core_auth_user",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Alias",
                table: "charging_station",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "charging_station",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_transactions_snapshot_IdTokenId",
                table: "transactions_snapshot",
                column: "IdTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_snapshot_IdTokenId1",
                table: "transactions_snapshot",
                column: "IdTokenId1");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_IdTokenId",
                table: "transactions_event",
                column: "IdTokenId");

            migrationBuilder.CreateIndex(
                name: "IX_transactions_event_IdTokenId1",
                table: "transactions_event",
                column: "IdTokenId1");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId",
                table: "transactions_event",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId1",
                table: "transactions_event",
                column: "IdTokenId1",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId",
                table: "transactions_snapshot",
                column: "IdTokenId",
                principalTable: "core_id_token",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId1",
                table: "transactions_snapshot",
                column: "IdTokenId1",
                principalTable: "core_id_token",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId",
                table: "transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_event_core_id_token_IdTokenId1",
                table: "transactions_event");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId",
                table: "transactions_snapshot");

            migrationBuilder.DropForeignKey(
                name: "FK_transactions_snapshot_core_id_token_IdTokenId1",
                table: "transactions_snapshot");

            migrationBuilder.DropIndex(
                name: "IX_transactions_snapshot_IdTokenId",
                table: "transactions_snapshot");

            migrationBuilder.DropIndex(
                name: "IX_transactions_snapshot_IdTokenId1",
                table: "transactions_snapshot");

            migrationBuilder.DropIndex(
                name: "IX_transactions_event_IdTokenId",
                table: "transactions_event");

            migrationBuilder.DropIndex(
                name: "IX_transactions_event_IdTokenId1",
                table: "transactions_event");

            migrationBuilder.DropColumn(
                name: "IdTokenId",
                table: "transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "IdTokenId1",
                table: "transactions_snapshot");

            migrationBuilder.DropColumn(
                name: "IdTokenId",
                table: "transactions_event");

            migrationBuilder.DropColumn(
                name: "IdTokenId1",
                table: "transactions_event");

            migrationBuilder.DropColumn(
                name: "IsUnderTx",
                table: "core_id_token");

            migrationBuilder.DropColumn(
                name: "ActiveCredit",
                table: "core_auth_user");

            migrationBuilder.DropColumn(
                name: "Alias",
                table: "charging_station");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "charging_station");

            migrationBuilder.AddColumn<string>(
                name: "IdToken",
                table: "transactions_event",
                type: "text",
                nullable: true);
        }
    }
}
