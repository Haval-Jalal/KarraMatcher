namespace KarraMatcher.Infrastructure.Persistence.Seed;

/// <summary>
/// Startdata hämtad ur den handbyggda föregångaren. Tiderna står i svensk lokaltid,
/// precis som tränarna läser dem — konverteringen till UTC sker i seedern.
/// </summary>
internal static class SeedData
{
    public const string ClubName = "Kärra";
    public const string ClubSlug = "karra";
    public const string AgeGroupName = "P2016";
    public const string Season = "2026";

    public sealed record TeamRow(string Name, string Slug, string ColorHex);

    public sealed record VenueRow(
        string Name, string Address, double Latitude, double Longitude, bool IsHome);

    public sealed record MatchRow(
        string Date, string Time, string TeamSlug, string Opponent, string VenueName);

    public static IReadOnlyList<TeamRow> Teams { get; } =
    [
        new("Gul", "gul", "#D9A21B"),
        new("Blå", "bla", "#1E3F8A"),
        new("Vit", "vit", "#D9D9D9"),
        new("Svart", "svart", "#161616"),
    ];

    /// <summary>
    /// Namnet är hela strängen som står i kallelsen, plannumret inkluderat — det är vad
    /// föräldern letar efter på plats.
    ///
    /// <para>
    /// <b>Adressen</b> driver kartlänken. Den anges som platsnamn och inte som gatuadress,
    /// eftersom det är så kartappar hittar en idrottsplats: "Klarebergsvallen, Kärra,
    /// Göteborg" ger en träff på anläggningen, medan "Klarebergsvallen 3" inte ger någon
    /// träff alls — trean är plannumret, inte ett gatunummer.
    /// </para>
    ///
    /// <para>
    /// <b>Koordinaterna</b> driver enbart väderprognosen. De var tidigare avrundade till
    /// två decimaler och låg upp till 2,2 km fel, vilket vid kusten är skillnaden mellan
    /// regn och uppehåll. Värdena nedan är verifierade mot OpenStreetMap och pekar på
    /// själva anläggningen.
    /// </para>
    /// </summary>
    public static IReadOnlyList<VenueRow> Venues { get; } =
    [
        new("Klarebergsvallen 3", "Klarebergsvallen, Kärra, Göteborg", 57.7996, 11.9840, true),
        new("Fjärdingsplan 11", "Fjärdingsplan, Lundby, Göteborg", 57.7241, 11.9390, false),
        new("Länsmansgårdens IP 22", "Länsmansgårdens IP, Göteborg", 57.7311, 11.8856, false),
        new("Kareby Hed 11", "Kareby Hed, Kareby, Kungälv", 57.9032, 11.9279, false),
        new("Krokängsplan 12", "Krokängsplan, Eriksberg, Göteborg", 57.7031, 11.9102, false),

        // Kode IP är inte verifierad. OpenStreetMap har fyra namnlösa fotbollsplaner i
        // Kode och ingen namngiven idrottsplats, så värdet nedan är det gamla avrundade.
        // Att gissa hade varit sämre än att låta det stå kvar och vara känt fel — se #110.
        new("Kode IP 31", "Kode IP, Kode, Kungälv", 57.96, 11.87, false),

        new("Prästängen 31", "Prästängen, Öckerö", 57.7166, 11.6410, false),
    ];

    public static IReadOnlyList<MatchRow> Matches { get; } =
    [
        new("2026-08-29", "14:30", "bla", "Finlandia Pallo AIF Vit", "Klarebergsvallen 3"),
        new("2026-08-30", "12:00", "vit", "IK Zenith Röd P2016", "Klarebergsvallen 3"),
        new("2026-08-30", "13:15", "gul", "Finlandia Pallo AIF Blå", "Klarebergsvallen 3"),
        new("2026-08-30", "14:30", "svart", "Lundby IF P2016 Röd", "Klarebergsvallen 3"),
        new("2026-09-02", "17:15", "svart", "Torslanda IK Tigers", "Klarebergsvallen 3"),
        new("2026-09-05", "15:30", "gul", "Lundby IF P2016 Grön", "Fjärdingsplan 11"),
        new("2026-09-05", "17:00", "vit", "Lundby IF P2016 Blå", "Fjärdingsplan 11"),
        new("2026-09-06", "15:00", "svart", "Solväders FC P2016", "Länsmansgårdens IP 22"),
        new("2026-09-06", "15:30", "bla", "Lundby IF P2016 Vit", "Fjärdingsplan 11"),
        new("2026-09-12", "13:15", "vit", "Kareby IS 1", "Kareby Hed 11"),
        new("2026-09-12", "17:00", "bla", "Torslanda IK Bears", "Klarebergsvallen 3"),
        new("2026-09-13", "14:30", "svart", "Lundby IF P2016 Blå", "Klarebergsvallen 3"),
        new("2026-09-13", "15:45", "gul", "Torslanda IK Ducks", "Klarebergsvallen 3"),
        new("2026-09-20", "12:00", "svart", "Kareby IS Blå", "Klarebergsvallen 3"),
        new("2026-09-20", "13:15", "vit", "Kareby IS Röd", "Klarebergsvallen 3"),
        new("2026-09-20", "13:30", "bla", "Eriksbergs IF 2", "Krokängsplan 12"),
        new("2026-09-20", "14:45", "gul", "Eriksbergs IF 1", "Krokängsplan 12"),
        new("2026-09-26", "12:15", "svart", "Kode IF P16:2", "Kode IP 31"),
        new("2026-09-26", "13:30", "vit", "Kode IF P16:1", "Kode IP 31"),
        new("2026-09-27", "13:15", "gul", "IK Zenith", "Klarebergsvallen 3"),
        new("2026-09-27", "14:30", "bla", "IK Zenith Grön P2016", "Klarebergsvallen 3"),
        new("2026-10-04", "12:00", "svart", "Nödinge SK Fotboll NSK 2", "Klarebergsvallen 3"),
        new("2026-10-04", "13:15", "vit", "Nödinge SK Fotboll NSK 1", "Klarebergsvallen 3"),
        new("2026-10-04", "13:30", "bla", "Öckerö IF Vit", "Prästängen 31"),
        new("2026-10-04", "14:45", "gul", "Öckerö IF Vinröd", "Prästängen 31"),
    ];
}
