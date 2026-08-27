using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PendingManagerTransferEffectiveAt",
                table: "Buildings",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When the pending manager transfer takes effect, unless cancelled first");

            migrationBuilder.AddColumn<DateTime>(
                name: "PendingManagerTransferInitiatedAt",
                table: "Buildings",
                type: "timestamp with time zone",
                nullable: true,
                comment: "When the pending manager transfer was initiated");

            migrationBuilder.AddColumn<string>(
                name: "PendingManagerTransferToUserId",
                table: "Buildings",
                type: "text",
                nullable: true,
                comment: "User identifier of the pending HouseManager successor, if a transfer is in progress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PendingManagerTransferEffectiveAt",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "PendingManagerTransferInitiatedAt",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "PendingManagerTransferToUserId",
                table: "Buildings");
        }
    }
}
