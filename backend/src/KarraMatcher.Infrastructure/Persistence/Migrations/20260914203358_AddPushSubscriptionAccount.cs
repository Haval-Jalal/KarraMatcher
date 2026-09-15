using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPushSubscriptionAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "PushSubscriptions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_AccountId",
                table: "PushSubscriptions",
                column: "AccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_PushSubscriptions_Accounts_AccountId",
                table: "PushSubscriptions",
                column: "AccountId",
                principalTable: "Accounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PushSubscriptions_Accounts_AccountId",
                table: "PushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_AccountId",
                table: "PushSubscriptions");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "PushSubscriptions");
        }
    }
}
