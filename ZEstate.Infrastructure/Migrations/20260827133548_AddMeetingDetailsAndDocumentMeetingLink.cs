using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeetingDetailsAndDocumentMeetingLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Agenda",
                table: "Meetings",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                comment: "Agenda items, one per line");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Meetings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "Physical location, if not (or in addition to) a video link");

            migrationBuilder.AddColumn<int>(
                name: "MeetingId",
                table: "Documents",
                type: "integer",
                nullable: true,
                comment: "Meeting identifier, if document is minutes/protocol for a meeting");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_MeetingId",
                table: "Documents",
                column: "MeetingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Documents_Meetings_MeetingId",
                table: "Documents",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Documents_Meetings_MeetingId",
                table: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_Documents_MeetingId",
                table: "Documents");

            migrationBuilder.DropColumn(
                name: "Agenda",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Meetings");

            migrationBuilder.DropColumn(
                name: "MeetingId",
                table: "Documents");
        }
    }
}
