using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatReactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatReactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReactedByAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Emoji = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatReactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatReactions_Accounts_ReactedByAccountId",
                        column: x => x.ReactedByAccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChatReactions_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatReactions_MessageId_ReactedByAccountId_Emoji",
                table: "ChatReactions",
                columns: new[] { "MessageId", "ReactedByAccountId", "Emoji" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatReactions_ReactedByAccountId",
                table: "ChatReactions",
                column: "ReactedByAccountId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatReactions");
        }
    }
}
