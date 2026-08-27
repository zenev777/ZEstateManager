using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddApartmentTransfer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreviousOwnerUserId",
                table: "Obligations",
                type: "text",
                nullable: true,
                comment: "Set at apartment-transfer time if the manager chose to keep this debt with the departing owner rather than passing it to the new one");

            migrationBuilder.CreateTable(
                name: "ApartmentTransferLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Apartment transfer log entry identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false, comment: "Apartment identifier"),
                    PreviousOwnerUserId = table.Column<string>(type: "text", nullable: true, comment: "User identifier of the owner who left, if there was an active one"),
                    TransferredByUserId = table.Column<string>(type: "text", nullable: false, comment: "User identifier of the house manager who performed the transfer"),
                    DebtHandling = table.Column<int>(type: "integer", nullable: false, comment: "How outstanding debts at the time of transfer were handled"),
                    OutstandingBalanceAtTransfer = table.Column<decimal>(type: "numeric", nullable: false, comment: "Sum of outstanding obligation balances at the moment of transfer, for audit"),
                    TransferredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date/time the transfer was recorded")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentTransferLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApartmentTransferLogs_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApartmentTransferLogs_AspNetUsers_PreviousOwnerUserId",
                        column: x => x.PreviousOwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ApartmentTransferLogs_AspNetUsers_TransferredByUserId",
                        column: x => x.TransferredByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_PreviousOwnerUserId",
                table: "Obligations",
                column: "PreviousOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentTransferLogs_ApartmentId",
                table: "ApartmentTransferLogs",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentTransferLogs_PreviousOwnerUserId",
                table: "ApartmentTransferLogs",
                column: "PreviousOwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentTransferLogs_TransferredByUserId",
                table: "ApartmentTransferLogs",
                column: "TransferredByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Obligations_AspNetUsers_PreviousOwnerUserId",
                table: "Obligations",
                column: "PreviousOwnerUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Obligations_AspNetUsers_PreviousOwnerUserId",
                table: "Obligations");

            migrationBuilder.DropTable(
                name: "ApartmentTransferLogs");

            migrationBuilder.DropIndex(
                name: "IX_Obligations_PreviousOwnerUserId",
                table: "Obligations");

            migrationBuilder.DropColumn(
                name: "PreviousOwnerUserId",
                table: "Obligations");
        }
    }
}
