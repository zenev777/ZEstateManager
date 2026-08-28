using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashLedgerRepairLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepairId",
                table: "CashLedgerEntries",
                type: "integer",
                nullable: true,
                comment: "The repair this withdrawal was made for, if any");

            migrationBuilder.CreateIndex(
                name: "IX_CashLedgerEntries_RepairId",
                table: "CashLedgerEntries",
                column: "RepairId");

            migrationBuilder.AddForeignKey(
                name: "FK_CashLedgerEntries_Repairs_RepairId",
                table: "CashLedgerEntries",
                column: "RepairId",
                principalTable: "Repairs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CashLedgerEntries_Repairs_RepairId",
                table: "CashLedgerEntries");

            migrationBuilder.DropIndex(
                name: "IX_CashLedgerEntries_RepairId",
                table: "CashLedgerEntries");

            migrationBuilder.DropColumn(
                name: "RepairId",
                table: "CashLedgerEntries");
        }
    }
}
