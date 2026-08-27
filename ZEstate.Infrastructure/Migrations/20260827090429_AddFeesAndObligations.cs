using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeesAndObligations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "Period",
                table: "Obligations",
                type: "timestamp with time zone",
                nullable: true,
                comment: "First day of the month this obligation was generated for (Monthly fees only, used to prevent duplicate generation); null for OneTime fees");

            migrationBuilder.AddColumn<int>(
                name: "Frequency",
                table: "Fees",
                type: "integer",
                nullable: false,
                defaultValue: 1, // FeeFrequency.Monthly, matching the model's default
                comment: "How often the fee recurs: OneTime or Monthly");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Period",
                table: "Obligations");

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "Fees");
        }
    }
}
