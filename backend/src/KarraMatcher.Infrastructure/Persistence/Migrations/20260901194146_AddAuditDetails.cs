using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Details",
                table: "AuditEntries",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubjectId",
                table: "AuditEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_SubjectId",
                table: "AuditEntries",
                column: "SubjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_SubjectId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "Details",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "SubjectId",
                table: "AuditEntries");
        }
    }
}
