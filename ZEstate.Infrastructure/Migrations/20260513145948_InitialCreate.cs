using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ZEstate.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    LastName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Buildings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Building name"),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Building address"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the building was added to the system"),
                    InviteCode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "Unique invite code for residents to join")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Buildings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    RoleId = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Apartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Apartment identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    Number = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, comment: "Apartment number"),
                    Floor = table.Column<int>(type: "integer", nullable: false, comment: "Floor number"),
                    IdealParts = table.Column<decimal>(type: "numeric", nullable: false, comment: "Ideal parts percentage of the building"),
                    Budget = table.Column<decimal>(type: "numeric", nullable: false, comment: "Apartment budget balance")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Apartments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Apartments_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Meetings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Meeting identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Meeting title"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Meeting description"),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Meeting start date and time"),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Meeting end date and time"),
                    MeetUrl = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, comment: "Google Meet link for the meeting"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Current meeting status")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Meetings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Meetings_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Repairs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Repair identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Repair title"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Repair description"),
                    Budget = table.Column<decimal>(type: "numeric", nullable: false, comment: "Repair budget"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Current repair status"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the repair was created")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repairs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repairs_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApartmentUsers",
                columns: table => new
                {
                    ApartmentId = table.Column<int>(type: "integer", nullable: false, comment: "Apartment identifier"),
                    UserId = table.Column<string>(type: "text", nullable: false, comment: "User identifier"),
                    Role = table.Column<int>(type: "integer", nullable: false, comment: "Role of the user in this apartment"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, comment: "Whether the user is currently active in this apartment"),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the user joined the apartment")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApartmentUsers", x => new { x.UserId, x.ApartmentId });
                    table.ForeignKey(
                        name: "FK_ApartmentUsers_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApartmentUsers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Vote identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MeetingId = table.Column<int>(type: "integer", nullable: false, comment: "Meeting identifier"),
                    UserId = table.Column<string>(type: "text", nullable: false, comment: "User identifier"),
                    Value = table.Column<int>(type: "integer", nullable: false, comment: "Vote value: Yes, No or Abstain"),
                    VotedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date and time when the vote was cast")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Votes_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votes_Meetings_MeetingId",
                        column: x => x.MeetingId,
                        principalTable: "Meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Document identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    RepairId = table.Column<int>(type: "integer", nullable: true, comment: "Repair identifier, if document belongs to a repair"),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false, comment: "File storage path"),
                    FileName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, comment: "Original file name"),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Document type: Protocol, Contract, Invoice or Other"),
                    Access = table.Column<int>(type: "integer", nullable: false, comment: "Access level: All or ManagerOnly"),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the document was uploaded")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Documents_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Documents_Repairs_RepairId",
                        column: x => x.RepairId,
                        principalTable: "Repairs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Fees",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Fee identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BuildingId = table.Column<int>(type: "integer", nullable: false, comment: "Building identifier"),
                    RepairId = table.Column<int>(type: "integer", nullable: true, comment: "Repair identifier, if fee is related to a repair"),
                    Title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false, comment: "Fee title"),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true, comment: "Fee description"),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false, comment: "Fee amount"),
                    Type = table.Column<int>(type: "integer", nullable: false, comment: "Fee type: Fixed, PerIdealPart or Repair"),
                    DateFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date from which the fee is active"),
                    DateTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Date until which the fee is active"),
                    Priority = table.Column<int>(type: "integer", nullable: false, comment: "Fee priority"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the fee was created")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Fees_Buildings_BuildingId",
                        column: x => x.BuildingId,
                        principalTable: "Buildings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Fees_Repairs_RepairId",
                        column: x => x.RepairId,
                        principalTable: "Repairs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Obligations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Obligation identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ApartmentId = table.Column<int>(type: "integer", nullable: false, comment: "Apartment identifier"),
                    FeeId = table.Column<int>(type: "integer", nullable: false, comment: "Fee identifier"),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false, comment: "Amount due"),
                    Status = table.Column<int>(type: "integer", nullable: false, comment: "Current payment status"),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date when the obligation was created"),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true, comment: "Due date for the obligation")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Obligations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Obligations_Apartments_ApartmentId",
                        column: x => x.ApartmentId,
                        principalTable: "Apartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Obligations_Fees_FeeId",
                        column: x => x.FeeId,
                        principalTable: "Fees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false, comment: "Payment identifier")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ObligationId = table.Column<int>(type: "integer", nullable: false, comment: "Obligation identifier"),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false, comment: "Amount paid"),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, comment: "Date and time of payment"),
                    Method = table.Column<int>(type: "integer", nullable: false, comment: "Payment method used"),
                    TransactionId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, comment: "Stripe transaction identifier, null if paid manually")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_Obligations_ObligationId",
                        column: x => x.ObligationId,
                        principalTable: "Obligations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Apartments_BuildingId",
                table: "Apartments",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_ApartmentUsers_ApartmentId",
                table: "ApartmentUsers",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Documents_BuildingId",
                table: "Documents",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_RepairId",
                table: "Documents",
                column: "RepairId");

            migrationBuilder.CreateIndex(
                name: "IX_Fees_BuildingId",
                table: "Fees",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Fees_RepairId",
                table: "Fees",
                column: "RepairId");

            migrationBuilder.CreateIndex(
                name: "IX_Meetings_BuildingId",
                table: "Meetings",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_ApartmentId",
                table: "Obligations",
                column: "ApartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Obligations_FeeId",
                table: "Obligations",
                column: "FeeId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ObligationId",
                table: "Payments",
                column: "ObligationId");

            migrationBuilder.CreateIndex(
                name: "IX_Repairs_BuildingId",
                table: "Repairs",
                column: "BuildingId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_MeetingId",
                table: "Votes",
                column: "MeetingId");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_UserId",
                table: "Votes",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApartmentUsers");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "Obligations");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "Meetings");

            migrationBuilder.DropTable(
                name: "Apartments");

            migrationBuilder.DropTable(
                name: "Fees");

            migrationBuilder.DropTable(
                name: "Repairs");

            migrationBuilder.DropTable(
                name: "Buildings");
        }
    }
}
