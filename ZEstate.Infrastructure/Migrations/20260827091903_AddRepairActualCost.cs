using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRepairActualCost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Budget",
                table: "Repairs",
                type: "numeric",
                nullable: false,
                comment: "Planned repair budget",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldComment: "Repair budget");

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCost",
                table: "Repairs",
                type: "numeric",
                nullable: true,
                comment: "Actual cost once known; falls back to Budget for cost allocation until set");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActualCost",
                table: "Repairs");

            migrationBuilder.AlterColumn<decimal>(
                name: "Budget",
                table: "Repairs",
                type: "numeric",
                nullable: false,
                comment: "Repair budget",
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldComment: "Planned repair budget");
        }
    }
}
