using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJoinRequestRejectionReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "JoinRequests",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true,
                comment: "Optional reason the house manager gave when rejecting the request");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "JoinRequests");
        }
    }
}
