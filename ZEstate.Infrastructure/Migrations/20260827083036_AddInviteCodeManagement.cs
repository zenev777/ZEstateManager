using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInviteCodeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "InviteCodeActive",
                table: "Buildings",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                comment: "Whether the invite code currently accepts new registrations");

            migrationBuilder.AddColumn<DateTime>(
                name: "InviteCodeExpiresAt",
                table: "Buildings",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Optional expiration date/time for the invite code");

            migrationBuilder.AddColumn<int>(
                name: "InviteCodeMaxUses",
                table: "Buildings",
                type: "integer",
                nullable: true,
                comment: "Optional maximum number of times the invite code can be used");

            migrationBuilder.AddColumn<int>(
                name: "InviteCodeUseCount",
                table: "Buildings",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Number of times the current invite code has been used");

            migrationBuilder.CreateTable(
                name: "InviteCodeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Invite code log entry identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    ChangedByUserId = table.Column<string>(type: "text", nullable: false, comment: "User identifier of the house manager who made the change"),
                    Action = table.Column<int>(type: "integer", nullable: false, comment: "What kind of change was made to the invite code"),
                    OldCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, comment: "The invite code before the change, if applicable"),
                    NewCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true, comment: "The invite code after the change, if applicable"),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date/time the change was made")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InviteCodeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InviteCodeLogs_AspNetUsers_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InviteCodeLogs_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodeLogs_BuildingId",
                table: "InviteCodeLogs",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_InviteCodeLogs_ChangedByUserId",
                table: "InviteCodeLogs",
                column: "ChangedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InviteCodeLogs");

            migrationBuilder.DropColumn(
                name: "InviteCodeActive",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "InviteCodeExpiresAt",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "InviteCodeMaxUses",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "InviteCodeUseCount",
                table: "Buildings");
        }
    }
}
