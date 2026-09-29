using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Flyttar hemmaplanen från klubben till truppen (`#405`): varje trupp får sin egen, så en
    /// trupp-admin aldrig ändrar en annan trupps plan. Befintliga värden kopieras från klubben till
    /// var och en av dess trupper innan klubb-kolumnerna släpps, så inget går förlorat.
    /// </summary>
    public partial class MoveHomeVenueToTrupp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeVenueName",
                table: "AgeGroups",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeAddress",
                table: "AgeGroups",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeLatitude",
                table: "AgeGroups",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HomeLongitude",
                table: "AgeGroups",
                type: "double precision",
                nullable: true);

            // Kopiera klubbens plan till var och en av dess trupper innan kolumnerna släpps.
            migrationBuilder.Sql(
                """
                UPDATE "AgeGroups" AS a
                SET "HomeVenueName" = c."HomeVenueName",
                    "HomeAddress" = c."HomeAddress",
                    "HomeLatitude" = c."HomeLatitude",
                    "HomeLongitude" = c."HomeLongitude"
                FROM "Clubs" AS c
                WHERE a."ClubId" = c."Id";
                """);

            migrationBuilder.DropColumn(name: "HomeAddress", table: "Clubs");
            migrationBuilder.DropColumn(name: "HomeLatitude", table: "Clubs");
            migrationBuilder.DropColumn(name: "HomeLongitude", table: "Clubs");
            migrationBuilder.DropColumn(name: "HomeVenueName", table: "Clubs");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "HomeVenueName",
                table: "Clubs",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

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

            // Bästa möjliga återställning: klubben får en av sina truppers plan (per-trupp-planer
            // kan skilja sig, så en sammanslagning till en klubb-plan är med nödvändighet lossy).
            migrationBuilder.Sql(
                """
                UPDATE "Clubs" AS c
                SET "HomeVenueName" = a."HomeVenueName",
                    "HomeAddress" = a."HomeAddress",
                    "HomeLatitude" = a."HomeLatitude",
                    "HomeLongitude" = a."HomeLongitude"
                FROM (
                    SELECT DISTINCT ON ("ClubId")
                        "ClubId", "HomeVenueName", "HomeAddress", "HomeLatitude", "HomeLongitude"
                    FROM "AgeGroups"
                    WHERE "HomeLatitude" IS NOT NULL
                    ORDER BY "ClubId", "Id"
                ) AS a
                WHERE c."Id" = a."ClubId";
                """);

            migrationBuilder.DropColumn(name: "HomeAddress", table: "AgeGroups");
            migrationBuilder.DropColumn(name: "HomeLatitude", table: "AgeGroups");
            migrationBuilder.DropColumn(name: "HomeLongitude", table: "AgeGroups");
            migrationBuilder.DropColumn(name: "HomeVenueName", table: "AgeGroups");
        }
    }
}
