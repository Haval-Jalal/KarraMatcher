using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddV2DomainModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamRoles_AccountId_TeamId_Role",
                table: "TeamRoles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeamRoles_LagKravsForTranare",
                table: "TeamRoles");

            migrationBuilder.AddColumn<Guid>(
                name: "AgeGroupId",
                table: "TeamRoles",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SportId",
                table: "AgeGroups",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "Children",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    LastInitial = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    AgeGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Children", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Children_AgeGroups_AgeGroupId",
                        column: x => x.AgeGroupId,
                        principalTable: "AgeGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Children_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Sports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Guardianships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrantedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guardianships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Guardianships_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Guardianships_Children_ChildId",
                        column: x => x.ChildId,
                        principalTable: "Children",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeamRoles_AccountId_TeamId_AgeGroupId_Role",
                table: "TeamRoles",
                columns: new[] { "AccountId", "TeamId", "AgeGroupId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamRoles_AgeGroupId",
                table: "TeamRoles",
                column: "AgeGroupId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeamRoles_ScopePassarRollen",
                table: "TeamRoles",
                sql: "(\"Role\" = 1 AND \"TeamId\" IS NOT NULL AND \"AgeGroupId\" IS NULL)\nOR (\"Role\" = 2 AND \"AgeGroupId\" IS NOT NULL AND \"TeamId\" IS NULL)\nOR (\"Role\" = 3 AND \"TeamId\" IS NULL AND \"AgeGroupId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_AgeGroups_SportId",
                table: "AgeGroups",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_Children_AgeGroupId",
                table: "Children",
                column: "AgeGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Children_TeamId",
                table: "Children",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Guardianships_AccountId_ChildId",
                table: "Guardianships",
                columns: new[] { "AccountId", "ChildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Guardianships_ChildId",
                table: "Guardianships",
                column: "ChildId");

            migrationBuilder.CreateIndex(
                name: "IX_Sports_Slug",
                table: "Sports",
                column: "Slug",
                unique: true);

            // Default-sporten för trupper som fanns före v2 (§190). SportId fick tomt värde när
            // kolumnen lades till; skapa Fotboll och backa in den på befintliga trupper INNAN
            // främmandenyckeln läggs till, annars pekar SportId på en sport som inte finns.
            // Seeden slår upp sporten på slug, så samma Fotboll-rad återanvänds.
            migrationBuilder.InsertData(
                table: "Sports",
                columns: new[] { "Id", "Name", "Slug" },
                values: new object[]
                {
                    new Guid("11111111-1111-1111-1111-111111111111"), "Fotboll", "fotboll",
                });

            migrationBuilder.Sql(
                "UPDATE \"AgeGroups\" SET \"SportId\" = '11111111-1111-1111-1111-111111111111' "
                + "WHERE \"SportId\" = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.AddForeignKey(
                name: "FK_AgeGroups_Sports_SportId",
                table: "AgeGroups",
                column: "SportId",
                principalTable: "Sports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeamRoles_AgeGroups_AgeGroupId",
                table: "TeamRoles",
                column: "AgeGroupId",
                principalTable: "AgeGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AgeGroups_Sports_SportId",
                table: "AgeGroups");

            migrationBuilder.DropForeignKey(
                name: "FK_TeamRoles_AgeGroups_AgeGroupId",
                table: "TeamRoles");

            migrationBuilder.DropTable(
                name: "Guardianships");

            migrationBuilder.DropTable(
                name: "Sports");

            migrationBuilder.DropTable(
                name: "Children");

            migrationBuilder.DropIndex(
                name: "IX_TeamRoles_AccountId_TeamId_AgeGroupId_Role",
                table: "TeamRoles");

            migrationBuilder.DropIndex(
                name: "IX_TeamRoles_AgeGroupId",
                table: "TeamRoles");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeamRoles_ScopePassarRollen",
                table: "TeamRoles");

            migrationBuilder.DropIndex(
                name: "IX_AgeGroups_SportId",
                table: "AgeGroups");

            migrationBuilder.DropColumn(
                name: "AgeGroupId",
                table: "TeamRoles");

            migrationBuilder.DropColumn(
                name: "SportId",
                table: "AgeGroups");

            migrationBuilder.CreateIndex(
                name: "IX_TeamRoles_AccountId_TeamId_Role",
                table: "TeamRoles",
                columns: new[] { "AccountId", "TeamId", "Role" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeamRoles_LagKravsForTranare",
                table: "TeamRoles",
                sql: "(\"Role\" = 1 AND \"TeamId\" IS NOT NULL) OR (\"Role\" = 2 AND \"TeamId\" IS NULL)");
        }
    }
}
