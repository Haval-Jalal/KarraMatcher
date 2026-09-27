namespace KarraMatcher.Domain.Cup;

/// <summary>
/// Ett cup-lag (`#335`): ett tillfälligt lag en admin bygger av de barn som anmält sig till en
/// cup.
///
/// <para>
/// Skilt från de permanenta färg-lagen (<see cref="Teams.Team"/>) och från trupp-profilen
/// (§KM.1): ett cup-lag finns bara för sin cup, har andra barn varje gång, och städas med cupen.
/// Därför en egen lättviktig tabell och inte en temporär <c>Team</c>-rad — så tillfälliga cup-lag
/// aldrig blandas in i scheman, kaskader eller statistik-vakter som gäller de riktiga lagen.
/// </para>
/// </summary>
public sealed class CupTeam
{
    public Guid Id { get; set; }

    /// <summary>
    /// Cup-händelsen laget hör till. Kaskad (§KM.6-anda): raderas cupen försvinner dess cup-lag.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>Lagets namn — admin skriver in det (t.ex. "Lag 1", "Röd"). Ingen barn-PII.</summary>
    public string Name { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    /// <summary>Största tillåtna längd på ett cup-lagsnamn.</summary>
    public const int MaxName = 60;
}
