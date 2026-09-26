using System.Collections;
using System.Reflection;

using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// §KM.1 — barnprofilen på servern får bära <b>bara</b> den vitlistade uppsättningen fält.
///
/// <para>
/// Tillåtet om ett barn: förnamn, efternamnets <em>initial</em>, tillhörighet till trupp och
/// lag, och ett valfritt tröjnummer. Förbjudet överallt: hela efternamnet, personnummer,
/// födelsedatum, adress, telefon, e-post, foto, hälsa, position. I dag lever den regeln på
/// disciplin — den här grinden gör den maskinell: lägger någon en kolonn som
/// <c>Personnummer</c>, <c>BirthDate</c> eller <c>LastName</c> på barnprofilen faller bygget,
/// oavsett om fältet kom via entiteten eller via en migration.
/// </para>
///
/// <para>
/// Vitlistan är avsiktligt <em>heltäckande</em>, inte en förbjuden-lista: ett nytt fält är
/// otillåtet tills någon uttryckligen lägger till det här — vilket enligt §KM.1 kräver ett
/// skrivet beslut i handoff i samma PR. Det är så en regel som bygger på en frånvaro överlever.
/// </para>
/// </summary>
public class ChildProfileAllowlistTests
{
    private const string ChildTable = "Children";

    /// <summary>
    /// De enda kolumnnamn en barnprofil får ha. Håll den i takt med §KM.1 — och bara efter ett
    /// skrivet beslut i docs/PROJEKT-HANDOFF.md.
    /// </summary>
    private static readonly HashSet<string> Allowed = new(StringComparer.Ordinal)
    {
        "Id",
        "FirstName",
        "LastInitial",
        "AgeGroupId", // truppen
        "TeamId", // laget (färgen), valfritt tills sorterat
        "ShirtNumber", // valfritt tröjnummer (§KM.1)
        "CreatedUtc",
    };

    [Fact]
    public void Barnprofilen_HarBaraDeVitlistadeFalten()
    {
        var columns = ColumnPropertyNames(typeof(Child));

        // Läsbarhet: om klassificeringen slutar hitta kolumner blir varje "inget otillåtet"
        // sant av fel skäl.
        Assert.Contains("FirstName", columns);
        Assert.Contains("LastInitial", columns);

        var offenders = columns.Where(name => !Allowed.Contains(name)).ToArray();

        Assert.True(offenders.Length == 0, ForbiddenFieldMessage(nameof(Child), offenders));
    }

    [Fact]
    public void IngenMigration_GerBarnprofilenEnOtillatenKolumn()
    {
        var columns = Migrations()
            .SelectMany(m => ColumnsOnTable(m, ChildTable).Select(name => (Migration: m, Name: name)))
            .ToArray();

        // Läsbarhet: bevisa att barnbordet faktiskt lästes, annars vaktar testet ingenting.
        Assert.Contains(columns, c => c.Name == "FirstName");

        var offenders = columns
            .Where(c => !Allowed.Contains(c.Name))
            .Select(c => $"{c.Migration.GetType().Name}: {c.Name}")
            .ToArray();

        Assert.True(offenders.Length == 0, ForbiddenFieldMessage($"{ChildTable}-tabellen", offenders));
    }

    // ---- Självtester: bevisar att grinden ser ett förbjudet fält -----------------------

    [Fact]
    public void Vakten_FangarEttForbjudetFaltPaEntiteten()
    {
        var columns = ColumnPropertyNames(typeof(FakeChildWithSurname));

        Assert.Contains("Personnummer", columns);
        Assert.Contains("LastName", columns);
        Assert.DoesNotContain(columns, name => name == "Team"); // navigering räknas inte
    }

    [Fact]
    public void Vakten_FangarEnForbjudenKolumnIEnMigration()
    {
        var columns = ColumnsOnTable(new FakeChildColumnMigration(), ChildTable).ToArray();

        Assert.Contains("BirthDate", columns);
        Assert.DoesNotContain("Personnummer", columns); // hör till en annan tabell i fejken
    }

    // ---- Avläsning --------------------------------------------------------------------

    /// <summary>
    /// Kolumn-bärande egenskaper: allt utom navigeringar (referens till en annan entitet eller
    /// en samling). En navigering är en relation, inte en kolumn på den här tabellen.
    /// </summary>
    private static string[] ColumnPropertyNames(Type entity) =>
        [.. entity
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !IsNavigation(p.PropertyType))
            .Select(p => p.Name)];

    private static bool IsNavigation(Type type)
    {
        if (type == typeof(string))
        {
            return false;
        }

        // En samling (t.ex. ICollection<Guardianship>) är en navigering.
        if (typeof(IEnumerable).IsAssignableFrom(type))
        {
            return true;
        }

        // En referens till en annan domän-entitet (AgeGroup, Team) är en navigering.
        return type.Assembly == typeof(Child).Assembly;
    }

    private static IEnumerable<string> ColumnsOnTable(Migration migration, string table)
    {
        foreach (var operation in migration.UpOperations)
        {
            switch (operation)
            {
                case CreateTableOperation create when create.Name == table:
                    foreach (var column in create.Columns)
                    {
                        yield return column.Name;
                    }

                    break;

                case AddColumnOperation add when add.Table == table:
                    yield return add.Name;
                    break;

                case RenameColumnOperation rename when rename.Table == table:
                    yield return rename.NewName;
                    break;

                default:
                    break;
            }
        }
    }

    private static Migration[] Migrations() =>
        [.. typeof(KarraMatcherDbContext).Assembly
            .GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && !t.IsAbstract)
            .Where(t => t.GetCustomAttribute<MigrationAttribute>() is not null)
            .Select(t => (Migration)Activator.CreateInstance(t)!)
            .OrderBy(
                m => m.GetType().GetCustomAttribute<MigrationAttribute>()!.Id,
                StringComparer.Ordinal)];

    private static string ForbiddenFieldMessage(string where, string[] offenders) =>
        $"Fält på {where} som inte finns i §KM.1-vitlistan:{Environment.NewLine}"
            + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
            + $"{Environment.NewLine}§KM.1: en barnprofil får bara bära förnamn, efternamnets "
            + "initial, trupp/lag och ett valfritt tröjnummer. Är fältet verkligen nödvändigt "
            + "krävs ett skrivet beslut i docs/PROJEKT-HANDOFF.md under Viktiga beslut, i samma "
            + "PR — och att namnet läggs till i vitlistan här.";

    /// <summary>En påhittad barnprofil som bryter §KM.1. Finns bara för att bevisa att grinden ser den.</summary>
    private sealed class FakeChildWithSurname
    {
        public Guid Id { get; set; }

        public string FirstName { get; set; } = string.Empty;

        public string LastName { get; set; } = string.Empty;

        public string Personnummer { get; set; } = string.Empty;

        public Team? Team { get; set; }
    }

    private sealed class FakeChildColumnMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "BirthDate", table: ChildTable, nullable: false);
            migrationBuilder.AddColumn<string>(name: "Personnummer", table: "SomethingElse", nullable: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("BirthDate", ChildTable);
        }
    }
}
