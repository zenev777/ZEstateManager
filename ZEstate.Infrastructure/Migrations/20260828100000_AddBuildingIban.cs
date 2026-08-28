using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingIban : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Iban",
                table: "Buildings",
                type: "character varying(34)",
                maxLength: 34,
                nullable: true,
                comment: "IBAN the building receives online (Stripe) payments' payouts to - required before online payment can be offered to residents");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Iban",
                table: "Buildings");
        }
    }
}
