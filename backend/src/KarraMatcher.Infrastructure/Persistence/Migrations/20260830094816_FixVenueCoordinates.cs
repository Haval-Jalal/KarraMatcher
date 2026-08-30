using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KarraMatcher.Infrastructure.Persistence.Migrations;

/// <summary>
/// Rättar spelplatsernas koordinater.
///
/// <para>
/// De var avrundade till två decimaler och låg upp till 2,2 kilometer fel. Koordinaterna
/// driver väderprognosen, och två kilometer är vid kusten skillnaden mellan regn och
/// uppehåll — Öckerö och Kungälv ligger båda vid vatten. Värdena här är verifierade mot
/// OpenStreetMap och pekar på själva anläggningen.
/// </para>
///
/// <para>
/// En datamigration och inte en ny körning av startdatan: seeden är avstängd i drift
/// (<c>Database__SeedOnStartup=false</c>) eftersom den är idempotent per naturlig nyckel
/// och skulle återuppliva matcher som en tränare tagit bort. Uppdateringen sker därför
/// kontrollerat och versionerat, som varje annan schemaändring.
/// </para>
///
/// <para>
/// Uppdateringen matchar på <c>Name</c> och inte på id: id:na genereras vid seedning och
/// skiljer sig mellan miljöer. Raderar någon en spelplats påverkas ingenting, eftersom
/// <c>UPDATE</c> utan träff är en tom operation.
/// </para>
///
/// <para>
/// <c>Kode IP 31</c> saknas med flit. OpenStreetMap har fyra namnlösa fotbollsplaner i Kode
/// och ingen namngiven idrottsplats, så den raden lämnas orörd tills någon som känner
/// platsen kan peka ut rätt plan. Ett gissat värde hade sett lika rätt ut som ett verifierat
/// och varit omöjligt att skilja från det efteråt.
/// </para>
/// </summary>
public partial class FixVenueCoordinates : Migration
{
    /// <summary>Verifierade positioner: namn, latitud, longitud.</summary>
    private static readonly (string Name, double Latitude, double Longitude)[] Corrected =
    [
        ("Klarebergsvallen 3", 57.7996, 11.9840),
        ("Fjärdingsplan 11", 57.7241, 11.9390),
        ("Länsmansgårdens IP 22", 57.7311, 11.8856),
        ("Kareby Hed 11", 57.9032, 11.9279),
        ("Krokängsplan 12", 57.7031, 11.9102),
        ("Prästängen 31", 57.7166, 11.6410),
    ];

    /// <summary>De avrundade värden som fanns före rättelsen, för Down.</summary>
    private static readonly (string Name, double Latitude, double Longitude)[] Previous =
    [
        ("Klarebergsvallen 3", 57.78, 11.99),
        ("Fjärdingsplan 11", 57.72, 11.93),
        ("Länsmansgårdens IP 22", 57.73, 11.90),
        ("Kareby Hed 11", 57.90, 11.95),
        ("Krokängsplan 12", 57.71, 11.92),
        ("Prästängen 31", 57.71, 11.65),
    ];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        Apply(migrationBuilder, Corrected);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        Apply(migrationBuilder, Previous);
    }

    private static void Apply(
        MigrationBuilder migrationBuilder,
        (string Name, double Latitude, double Longitude)[] rows)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        foreach (var (name, latitude, longitude) in rows)
        {
            // UpdateData parametriserar värdena åt oss. Att bygga SQL-strängar för hand
            // hade varit onödigt och dessutom fel ställe att öva injektionsrisker på.
            migrationBuilder.UpdateData(
                table: "Venues",
                keyColumn: "Name",
                keyValue: name,
                columns: ["Latitude", "Longitude"],
                values: [latitude, longitude]);
        }
    }
}
