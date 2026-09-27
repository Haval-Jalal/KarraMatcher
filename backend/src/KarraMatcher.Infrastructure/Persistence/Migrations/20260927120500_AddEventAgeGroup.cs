using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAgeGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Steg 1: lägg kolumnen nullbar tillfälligt så befintliga rader kan backfyllas.
            // (EF:s standard vore NOT NULL med Guid.Empty som default — det skulle bryta mot
            // FK:n nedan, eftersom ingen trupp har id Guid.Empty.)
            migrationBuilder.AddColumn<Guid>(
                name: "AgeGroupId",
                table: "Events",
                type: "uuid",
                nullable: true);

            // Steg 2: backfyll. En händelses trupp är dess lags trupp — all data hör till
            // truppen, laget är bara en uppdelning (`#332`). Idempotent och säker: bara rader
            // vars lag finns kvar får ett värde, och alla befintliga händelser har ett lag.
            migrationBuilder.Sql(
                @"UPDATE ""Events"" e SET ""AgeGroupId"" = t.""AgeGroupId"" FROM ""Teams"" t WHERE e.""TeamId"" = t.""Id"";");

            // Steg 3: nu har varje rad ett värde — gör kolumnen obligatorisk.
            migrationBuilder.AlterColumn<Guid>(
                name: "AgeGroupId",
                table: "Events",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Events_AgeGroupId",
                table: "Events",
                column: "AgeGroupId");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_AgeGroups_AgeGroupId",
                table: "Events",
                column: "AgeGroupId",
                principalTable: "AgeGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Events_AgeGroups_AgeGroupId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Events_AgeGroupId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "AgeGroupId",
                table: "Events");
        }
    }
}
