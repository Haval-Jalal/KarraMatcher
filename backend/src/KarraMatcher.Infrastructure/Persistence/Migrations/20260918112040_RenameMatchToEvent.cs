using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Match → Event (`#198`). Behåller data: tabellen döps om i stället för att skapas på nytt,
    /// och befintliga matcher får Type='Match' via kolumnens default. Närvaro och samåkning
    /// behåller sin MatchId-kolumn (pekar nu på Events) — bara främmande nyckelns namn byts.
    /// </summary>
    public partial class RenameMatchToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Släpp de beroende främmande nycklarna (gamla namn) medan principaltabellen döps om.
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceCalls_Matches_MatchId",
                table: "AttendanceCalls");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceResponses_Matches_MatchId",
                table: "AttendanceResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_CarpoolOffers_Matches_MatchId",
                table: "CarpoolOffers");

            // Döp om tabellen — raderna följer med. FK/PK/index behåller sina namn i Postgres,
            // så vi döper om dem uttryckligen så de matchar modellen (Events).
            migrationBuilder.RenameTable(name: "Matches", newName: "Events");

            migrationBuilder.Sql(
                "ALTER TABLE \"Events\" RENAME CONSTRAINT \"PK_Matches\" TO \"PK_Events\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Events\" RENAME CONSTRAINT \"FK_Matches_Teams_TeamId\" TO \"FK_Events_Teams_TeamId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Events\" RENAME CONSTRAINT \"FK_Matches_Venues_VenueId\" TO \"FK_Events_Venues_VenueId\";");

            migrationBuilder.RenameIndex(
                name: "IX_Matches_TeamId_KickoffUtc",
                newName: "IX_Events_TeamId_KickoffUtc",
                table: "Events");

            migrationBuilder.RenameIndex(
                name: "IX_Matches_VenueId",
                newName: "IX_Events_VenueId",
                table: "Events");

            // Ny typ. Default 'Match' fyller befintliga rader — det är migreringen av matcherna.
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Match");

            // Rubrik för träning/övrigt.
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Events",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            // Motståndare och hemma/borta gäller bara en match — gör dem nullbara.
            migrationBuilder.AlterColumn<string>(
                name: "OpponentName",
                table: "Events",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120);

            migrationBuilder.AlterColumn<bool>(
                name: "IsHome",
                table: "Events",
                type: "boolean",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            // Återinför de beroende främmande nycklarna, nu mot Events (nya namn).
            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceCalls_Events_MatchId",
                table: "AttendanceCalls",
                column: "MatchId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceResponses_Events_MatchId",
                table: "AttendanceResponses",
                column: "MatchId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CarpoolOffers_Events_MatchId",
                table: "CarpoolOffers",
                column: "MatchId",
                principalTable: "Events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceCalls_Events_MatchId",
                table: "AttendanceCalls");

            migrationBuilder.DropForeignKey(
                name: "FK_AttendanceResponses_Events_MatchId",
                table: "AttendanceResponses");

            migrationBuilder.DropForeignKey(
                name: "FK_CarpoolOffers_Events_MatchId",
                table: "CarpoolOffers");

            // Nollställ de rader som inte är matcher innan matchspecifika kolumner blir NOT NULL igen.
            migrationBuilder.Sql("DELETE FROM \"Events\" WHERE \"Type\" <> 'Match';");

            migrationBuilder.DropColumn(name: "Type", table: "Events");
            migrationBuilder.DropColumn(name: "Title", table: "Events");

            migrationBuilder.Sql(
                "UPDATE \"Events\" SET \"OpponentName\" = '' WHERE \"OpponentName\" IS NULL;");
            migrationBuilder.Sql(
                "UPDATE \"Events\" SET \"IsHome\" = false WHERE \"IsHome\" IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "OpponentName",
                table: "Events",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(120)",
                oldMaxLength: 120,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "IsHome",
                table: "Events",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldNullable: true);

            migrationBuilder.RenameIndex(
                name: "IX_Events_TeamId_KickoffUtc",
                newName: "IX_Matches_TeamId_KickoffUtc",
                table: "Events");

            migrationBuilder.RenameIndex(
                name: "IX_Events_VenueId",
                newName: "IX_Matches_VenueId",
                table: "Events");

            migrationBuilder.Sql(
                "ALTER TABLE \"Events\" RENAME CONSTRAINT \"PK_Events\" TO \"PK_Matches\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Events\" RENAME CONSTRAINT \"FK_Events_Teams_TeamId\" TO \"FK_Matches_Teams_TeamId\";");
            migrationBuilder.Sql(
                "ALTER TABLE \"Events\" RENAME CONSTRAINT \"FK_Events_Venues_VenueId\" TO \"FK_Matches_Venues_VenueId\";");

            migrationBuilder.RenameTable(name: "Events", newName: "Matches");

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceCalls_Matches_MatchId",
                table: "AttendanceCalls",
                column: "MatchId",
                principalTable: "Matches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AttendanceResponses_Matches_MatchId",
                table: "AttendanceResponses",
                column: "MatchId",
                principalTable: "Matches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CarpoolOffers_Matches_MatchId",
                table: "CarpoolOffers",
                column: "MatchId",
                principalTable: "Matches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
