using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PushSubscriptionPerDevice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_TeamId_Endpoint",
                table: "PushSubscriptions");

            // Per-enhet (`#332`-uppfoljning): tidigare fanns en rad per (lag, adress), sa samma
            // webblasare kunde ha flera rader med samma adress -- och fick en notis per rad. Innan
            // det unika indexet pa adressen kan skapas maste dubbletterna bort. Behall den nyaste
            // raden per adress (fallback: hogsta Id vid samma tid).
            migrationBuilder.Sql(
                """
                DELETE FROM "PushSubscriptions" a
                USING "PushSubscriptions" b
                WHERE a."Endpoint" = b."Endpoint"
                  AND (a."CreatedUtc" < b."CreatedUtc"
                       OR (a."CreatedUtc" = b."CreatedUtc" AND a."Id" < b."Id"));
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "TeamId",
                table: "PushSubscriptions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions",
                column: "Endpoint",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PushSubscriptions_Endpoint",
                table: "PushSubscriptions");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeamId",
                table: "PushSubscriptions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PushSubscriptions_TeamId_Endpoint",
                table: "PushSubscriptions",
                columns: new[] { "TeamId", "Endpoint" },
                unique: true);
        }
    }
}
