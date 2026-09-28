using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddChatReplyToMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReplyToMessageId",
                table: "ChatMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ReplyToMessageId",
                table: "ChatMessages",
                column: "ReplyToMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatMessages_ChatMessages_ReplyToMessageId",
                table: "ChatMessages",
                column: "ReplyToMessageId",
                principalTable: "ChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatMessages_ChatMessages_ReplyToMessageId",
                table: "ChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_ChatMessages_ReplyToMessageId",
                table: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "ReplyToMessageId",
                table: "ChatMessages");
        }
    }
}
