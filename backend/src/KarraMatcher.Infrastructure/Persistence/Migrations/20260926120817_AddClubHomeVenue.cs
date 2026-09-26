using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClubHomeVenue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeAddress",
                table: "Clubs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeLatitude",
                table: "Clubs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeLongitude",
                table: "Clubs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeVenueName",
                table: "Clubs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HomeAddress",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "HomeLatitude",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "HomeLongitude",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "HomeVenueName",
                table: "Clubs");
        }
    }
}
