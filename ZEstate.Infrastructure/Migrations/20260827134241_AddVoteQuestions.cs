using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVoteQuestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Meetings_MeetingId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_MeetingId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "MeetingId",
                table: "Votes");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Votes",
                type: "text",
                nullable: false,
                comment: "User identifier of whoever cast the vote",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "User identifier");

            migrationBuilder.AddColumn<int>(
                name: "ApartmentId",
                table: "Votes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Apartment identifier this vote is cast on behalf of");

            migrationBuilder.AddColumn<int>(
                name: "VoteQuestionId",
                table: "Votes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Vote question identifier");

            migrationBuilder.CreateTable(
                name: "VoteQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Vote question identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false, comment: "Meeting identifier"),
                    Question = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "The question being voted on"),
                    StartAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "When voting opens"),
                    EndAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "When voting closes"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the question was created")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VoteQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VoteQuestions_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_ApartmentId",
                table: "Votes",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_VoteQuestionId_ApartmentId",
                table: "Votes",
                columns: new[] { "VoteQuestionId", "ApartmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VoteQuestions_MeetingId",
                table: "VoteQuestions",
                column: "MeetingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Apartments_ApartmentId",
                table: "Votes",
                column: "ApartmentId",
                principalTable: "Apartments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_VoteQuestions_VoteQuestionId",
                table: "Votes",
                column: "VoteQuestionId",
                principalTable: "VoteQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Apartments_ApartmentId",
                table: "Votes");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_VoteQuestions_VoteQuestionId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "VoteQuestions");

            migrationBuilder.DropIndex(
                name: "IX_Votes_ApartmentId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Votes_VoteQuestionId_ApartmentId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "ApartmentId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "VoteQuestionId",
                table: "Votes");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Votes",
                type: "text",
                nullable: false,
                comment: "User identifier",
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "User identifier of whoever cast the vote");

            migrationBuilder.AddColumn<int>(
                name: "MeetingId",
                table: "Votes",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                comment: "Meeting identifier");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_MeetingId",
                table: "Votes",
                column: "MeetingId");

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Meetings_MeetingId",
                table: "Votes",
                column: "MeetingId",
                principalTable: "Meetings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
