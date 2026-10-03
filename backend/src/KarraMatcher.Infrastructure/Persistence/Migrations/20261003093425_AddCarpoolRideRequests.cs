using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCarpoolRideRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CarpoolRideRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Open"),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarpoolRideRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarpoolRideRequests_Accounts_RequesterAccountId",
                        column: x => x.RequesterAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarpoolRideRequests_Events_MatchId",
                        column: x => x.MatchId,
                        principalTable: "Events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CarpoolRideOffers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RideRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Seats = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResponseMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CarpoolRideOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CarpoolRideOffers_Accounts_DriverAccountId",
                        column: x => x.DriverAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CarpoolRideOffers_CarpoolRideRequests_RideRequestId",
                        column: x => x.RideRequestId,
                        principalTable: "CarpoolRideRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CarpoolRideOffers_DriverAccountId",
                table: "CarpoolRideOffers",
                column: "DriverAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CarpoolRideOffers_RideRequestId_DriverAccountId",
                table: "CarpoolRideOffers",
                columns: new[] { "RideRequestId", "DriverAccountId" },
                unique: true,
                filter: "\"Status\" IN ('Pending', 'Accepted')");

            migrationBuilder.CreateIndex(
                name: "IX_CarpoolRideOffers_RideRequestId_Status",
                table: "CarpoolRideOffers",
                columns: new[] { "RideRequestId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CarpoolRideRequests_MatchId_Status",
                table: "CarpoolRideRequests",
                columns: new[] { "MatchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CarpoolRideRequests_RequesterAccountId",
                table: "CarpoolRideRequests",
                column: "RequesterAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CarpoolRideOffers");

            migrationBuilder.DropTable(
                name: "CarpoolRideRequests");
        }
    }
}
