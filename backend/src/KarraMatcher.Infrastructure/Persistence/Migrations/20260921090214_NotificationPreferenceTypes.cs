using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NotificationPreferenceTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Reminders",
                table: "NotificationPreferences",
                newName: "Kallelser");

            migrationBuilder.RenameColumn(
                name: "MatchChanges",
                table: "NotificationPreferences",
                newName: "EventChanges");

            // Befintliga rader = "allt på" (en rad finns bara för den som ändrat något), så
            // chatt-växeln börjar påslagen precis som de övriga.
            migrationBuilder.AddColumn<bool>(
                name: "Chat",
                table: "NotificationPreferences",
                type: "boolean",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Chat",
                table: "NotificationPreferences");

            migrationBuilder.RenameColumn(
                name: "Kallelser",
                table: "NotificationPreferences",
                newName: "Reminders");

            migrationBuilder.RenameColumn(
                name: "EventChanges",
                table: "NotificationPreferences",
                newName: "MatchChanges");
        }
    }
}
