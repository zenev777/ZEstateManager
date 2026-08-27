using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBuildingQuorumThreshold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "QuorumThresholdPercent",
                table: "Buildings",
                type: "numeric",
                nullable: false,
                defaultValue: 50m, // matches the model default (ЗУЕС) - EF would otherwise default existing buildings to 0
                comment: "Minimum percentage of ideal parts that must vote for a decision to have quorum (ЗУЕС default: 50)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuorumThresholdPercent",
                table: "Buildings");
        }
    }
}
