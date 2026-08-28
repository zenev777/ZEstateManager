using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CashLedgerEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Cash ledger entry identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    Account = table.Column<int>(type: "integer", nullable: false, comment: "Which account (Cash or Bank) this entry affects"),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false, comment: "Signed amount - positive increases the account balance, negative decreases it"),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Human-readable description of the movement"),
                    PaymentId = table.Column<int>(type: "integer", nullable: true, comment: "The resident payment that produced this entry, if any"),
                    TransferGroupId = table.Column<Guid>(type: "uuid", nullable: true, comment: "Links the two legs of one internal transfer between accounts"),
                    CreatedByUserId = table.Column<string>(type: "text", nullable: true, comment: "Who recorded this entry (null for entries produced automatically, e.g. Stripe webhook)"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date and time the entry was recorded")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashLedgerEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashLedgerEntries_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CashLedgerEntries_Payments_PaymentId",
                        column: x => x.PaymentId,
                        principalTable: "Payments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_BuildingId",
                table: "CashLedgerEntries",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_PaymentId",
                table: "CashLedgerEntries",
                column: "PaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashLedgerEntries");
        }
    }
}
