using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Optimistisk concurrency på refresh-token via Postgres <c>xmin</c> (#611). <c>xmin</c> är
    /// radens systemversion och finns redan på varje tabell, så det här är avsiktligt en
    /// <b>no-op mot databasen</b>: Npgsql känner igen systemkolumnen och genererar ingen DDL
    /// (verifierat — migrationens SQL är bara raden i <c>__EFMigrationsHistory</c>). Migrationen
    /// finns bara för att snapshot:en ska spegla att modellen nu har concurrency-tokenen. Ta inte
    /// bort "AddColumn"-raden i tron att den skapar en kolumn — det gör den inte.
    /// </summary>
    public partial class AddRefreshTokenConcurrencyToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Genererar ingen SQL: xmin är en Postgres-systemkolumn (se klassdoc ovan).
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "RefreshTokens",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                table: "RefreshTokens");
        }
    }
}
