namespace KarraMatcher.Domain.Cup;

/// <summary>
/// Ett anmält barn placerat i ett cup-lag (`#335`).
///
/// <para>
/// Kaskad både från cup-laget och från barnet (§KM.6): raderas laget, eller tas barnet bort ur
/// truppen, försvinner placeringen. Ett barn hör till <b>högst ett</b> cup-lag per cup — det
/// vaktas i tjänsten (en placering flyttar barnet i stället för att skapa en andra).
/// </para>
/// </summary>
public sealed class CupTeamMember
{
    public Guid Id { get; set; }

    /// <summary>Cup-laget barnet placerats i. Kaskad: raderas laget försvinner placeringen.</summary>
    public Guid CupTeamId { get; set; }

    /// <summary>Det placerade barnet. Kaskad (§KM.6): försvinner med barnet.</summary>
    public Guid ChildId { get; set; }

    public DateTime CreatedUtc { get; set; }
}
