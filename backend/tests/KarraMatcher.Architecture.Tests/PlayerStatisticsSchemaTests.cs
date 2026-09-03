using System.Reflection;

using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// §KM.2 — hålet som typnamnen inte täcker: kolumner och migrationer.
///
/// <para>
/// <c>PlayerStatisticsTests</c> vaktar typnamn och vilka tabeller som finns.
/// <c>PlayerStatisticsEndpointTests</c> vaktar routerna. Båda utgår från att en ny yta
/// föds med ett nytt <em>namn</em> — en <c>PlayerStatsDto</c>, en <c>/stats</c>-route.
/// </para>
///
/// <para>
/// Så går det sällan till. Det troliga är att ingen skapar något nytt alls: någon lägger
/// en kolumn <c>Goals</c> på en tabell som redan finns, eller skriver en migration som
/// skapar tabellen utan att en entitet någonsin dyker upp i <c>DbContext</c>. Ingen av de
/// befintliga reglerna hade sagt ett ord om det, och regeln hade urholkats utan att någon
/// märkte det — vilket är precis hur ett skydd som bygger på en frånvaro går förlorat.
/// </para>
///
/// <para>
/// Migrationerna läses som <b>operationer</b>, inte som text. En textsökning missar
/// <c>Sql("CREATE TABLE ...")</c> lika lätt som den larmar på ett kommentarsord;
/// operationerna är vad som faktiskt körs mot databasen.
/// </para>
/// </summary>
public class PlayerStatisticsSchemaTests
{
    /// <summary>
    /// Migrationerna i den ordning EF skulle köra dem, med sina operationer utvecklade.
    /// </summary>
    private static Migration[] Migrations() =>
        [.. typeof(KarraMatcherDbContext).Assembly
            .GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() is not null)
            .Select(t => (Migration)Activator.CreateInstance(t)!)
            .OrderBy(m => m.GetType().GetCustomAttribute<MigrationAttribute>()!.Id,
                StringComparer.Ordinal)];

    /// <summary>
    /// Varje namn en migration ger databasen: tabeller, kolumner, index och sekvenser.
    /// </summary>
    internal static IEnumerable<string> NamesIntroducedBy(Migration migration)
    {
        foreach (var operation in migration.UpOperations)
        {
            switch (operation)
            {
                case CreateTableOperation create:
                    yield return create.Name;

                    foreach (var column in create.Columns)
                    {
                        yield return column.Name;
                    }

                    break;

                case AddColumnOperation add:
                    yield return add.Name;
                    break;

                case RenameColumnOperation rename:
                    yield return rename.NewName;
                    break;

                case RenameTableOperation renameTable:
                    yield return renameTable.NewName ?? string.Empty;
                    break;

                case CreateIndexOperation index:
                    yield return index.Name;
                    break;

                case CreateSequenceOperation sequence:
                    yield return sequence.Name;
                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    /// Rå SQL i en migration. Går inte att läsa som ett namn, så den granskas för sig.
    /// </summary>
    private static IEnumerable<string> RawSqlIn(Migration migration) =>
        migration.UpOperations.OfType<SqlOperation>().Select(o => o.Sql);

    private static Type[] EntityTypes() =>
        [.. typeof(KarraMatcherDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.Name.StartsWith("DbSet", StringComparison.Ordinal))
            .Select(p => p.PropertyType.GetGenericArguments()[0])];

    [Fact]
    public void IngenMigration_InforNagotSomBarBarnstatistik()
    {
        var offenders = Migrations()
            .SelectMany(m => NamesIntroducedBy(m)
                .Where(PlayerStatisticsTests.NamesPlayerStatistics)
                .Select(name => $"{m.GetType().Name}: {name}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Migrationer som inför barnstatistik i databasen:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
                + $"{Environment.NewLine}§KM.2: spelarkortet lagras enbart på enheten. En "
                + "migration är den ena vägen till en tabell som DbContext aldrig nämner — "
                + "därför granskas den för sig. Behövs undantaget på riktigt krävs ett skrivet "
                + "beslut i docs/PROJEKT-HANDOFF.md under Viktiga beslut, i samma PR.");
    }

    [Fact]
    public void IngenMigration_SkaparBarnstatistikMedRaSql()
    {
        /*
         * En textsokning over migrationsfilerna hade missat det har lika latt som den larmat
         * pa ett kommentarsord. Ra SQL ar den enda operation vars innehall inte gar att lasa
         * som ett namn, sa den granskas som text -- men bara den.
         */
        var offenders = Migrations()
            .SelectMany(m => RawSqlIn(m)
                .Where(sql => PlayerStatisticsTests.SplitWords(sql)
                    .Any(word => PlayerStatisticsTests.NamesPlayerStatistics(word)))
                .Select(sql => $"{m.GetType().Name}: {sql}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Rå SQL som ser ut att röra barnstatistik:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
                + $"{Environment.NewLine}§KM.2: servern ska varken kunna ta emot, lagra eller "
                + "returnera den datan.");
    }

    [Fact]
    public void IngenEntitet_HarEttFaltSomBarBarnstatistik()
    {
        /*
         * Det troliga sattet regeln urholkas: ingen skapar en ny typ, nagon lagger ett falt
         * `Goals` pa en tabell som redan finns. Typnamnskontrollen hade inte sagt ett ord.
         */
        var offenders = EntityTypes()
            .SelectMany(t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => PlayerStatisticsTests.NamesPlayerStatistics(p.Name))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Fält som ser ut att bära barnstatistik:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
                + $"{Environment.NewLine}§KM.2: barnets statistik finns bara på familjens egen "
                + "telefon. En kolumn på en befintlig tabell är lika mycket en lagring av den "
                + "som en egen tabell.");
    }

    [Fact]
    public void Migrationerna_GarAttLasa()
    {
        /*
         * Skyddar reglerna ovan mot att ga grona for att ingen migration hittades alls --
         * en tom lista uppfyller varje pastaende om vad listan inte innehaller.
         */
        var migrations = Migrations();

        Assert.NotEmpty(migrations);
        Assert.Contains(migrations, m => m.GetType().Name == "InitialCreate");

        // Och att operationerna faktiskt utvecklas: en migration utan operationer hade
        // granskats lika grundligt som en tom fil.
        Assert.NotEmpty(migrations.SelectMany(NamesIntroducedBy));
    }

    [Fact]
    public void Entiteterna_GarAttLasa()
    {
        var entities = EntityTypes();

        Assert.NotEmpty(entities);
        Assert.NotEmpty(entities.SelectMany(t => t.GetProperties()));
    }

    // ---- Självtester: bevisar att avläsningen ser det den ska ------------------------

    [Fact]
    public void NamesIntroducedBy_TarMedTabellKolumnOchIndex()
    {
        var names = NamesIntroducedBy(new FakeMigration()).ToArray();

        Assert.Contains("PlayerStats", names, StringComparer.Ordinal);
        Assert.Contains("Goals", names, StringComparer.Ordinal);
        Assert.Contains("IX_PlayerStats_ChildId", names, StringComparer.Ordinal);
    }

    [Fact]
    public void NamesIntroducedBy_FangarEnKolumnPaEnBefintligTabell()
    {
        var names = NamesIntroducedBy(new FakeColumnMigration()).ToArray();

        Assert.Contains("Assists", names, StringComparer.Ordinal);
        Assert.Contains(names, PlayerStatisticsTests.NamesPlayerStatistics);
    }

    /// <summary>
    /// En påhittad migration som gör precis det §KM.2 förbjuder. Finns bara för att bevisa
    /// att avläsningen ovan ser en sådan — utan den vore det gröna testet ett påstående om
    /// att inget hittades, inte om att något hade hittats.
    /// </summary>
    private sealed class FakeMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerStats",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    Goals = table.Column<int>(nullable: false),
                },
                constraints: table => table.PrimaryKey("PK_PlayerStats", x => x.Id));

            migrationBuilder.CreateIndex(name: "IX_PlayerStats_ChildId", table: "PlayerStats", column: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable("PlayerStats");
        }
    }

    private sealed class FakeColumnMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(name: "Assists", table: "Matches", nullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("Assists", "Matches");
        }
    }
}
