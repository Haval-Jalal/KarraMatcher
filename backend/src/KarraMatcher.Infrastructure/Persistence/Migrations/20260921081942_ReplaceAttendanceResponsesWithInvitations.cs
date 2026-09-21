using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAttendanceResponsesWithInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceResponses");

            migrationBuilder.CreateTable(
                name: "AttendanceInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CallId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reply = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RespondedByAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    RespondedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceInvitations_AttendanceCalls_CallId",
                        column: x => x.CallId,
                        principalTable: "AttendanceCalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceInvitations_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceInvitations_CallId_ChildId",
                table: "AttendanceInvitations",
                columns: new[] { "CallId", "ChildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceInvitations_ChildId",
                table: "AttendanceInvitations",
                column: "ChildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AttendanceInvitations");

            migrationBuilder.CreateTable(
                name: "AttendanceResponses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AttendanceResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AttendanceResponses_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AttendanceResponses_Events_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceResponses_AccountId",
                table: "AttendanceResponses",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceResponses_MatchId_AccountId",
                table: "AttendanceResponses",
                columns: new[] { "MatchId", "AccountId" },
                unique: true);
        }
    }
}
