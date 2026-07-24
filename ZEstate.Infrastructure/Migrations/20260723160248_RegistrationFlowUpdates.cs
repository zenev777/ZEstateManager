using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RegistrationFlowUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagerId",
                table: "Buildings",
                type: "text",
                nullable: true,
                comment: "The house manager responsible for this building");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "JoinRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Join request identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    UserId = table.Column<string>(type: "text", nullable: false, comment: "User identifier"),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false, comment: "Apartment identifier the user wants to join"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Current status of the join request"),
                    RequestedRole = table.Column<int>(type: "integer", nullable: false, comment: "Role the user requested when joining (owner or resident/tenant)"),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, comment: "Optional note from the user"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the request was submitted"),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Date when the request was reviewed")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JoinRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JoinRequests_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JoinRequests_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Buildings_ManagerId",
                table: "Buildings",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_ApartmentId",
                table: "JoinRequests",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_BuildingId",
                table: "JoinRequests",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_JoinRequests_UserId",
                table: "JoinRequests",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Buildings_AspNetUsers_ManagerId",
                table: "Buildings",
                column: "ManagerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Buildings_AspNetUsers_ManagerId",
                table: "Buildings");

            migrationBuilder.DropTable(
                name: "JoinRequests");

            migrationBuilder.DropIndex(
                name: "IX_Buildings_ManagerId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "AspNetUsers");
        }
    }
}
