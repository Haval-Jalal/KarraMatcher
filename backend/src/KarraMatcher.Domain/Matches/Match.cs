using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Domain.Matches;

/// <summary>
/// En match. Avspark lagras alltid i UTC och visas i Europe/Stockholm (§KM.5) —
/// säsongen sträcker sig förbi sommartidsskiftet i oktober, och en match som visas
/// en timme fel är det som får folk att sluta lita på appen.
/// </summary>
public sealed class Match
{
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    public Team? Team { get; set; }

    /// <summary>Avspark i UTC. Kind måste vara <see cref="DateTimeKind.Utc"/>.</summary>
    public DateTime KickoffUtc { get; set; }

    public required string OpponentName { get; set; }

    public Guid VenueId { get; set; }

    public Venue? Venue { get; set; }

    /// <summary>Avvikande adress för just den här matchen. Tom = spelplatsens adress.</summary>
    public string? AddressOverride { get; set; }

    public bool IsHome { get; set; }

    public MatchStatus Status { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Ökas vid varje ändring. Utan det uppdaterar inte föräldrarnas
    /// kalenderprenumerationer sig när en match flyttas (§KM.4).
    /// </summary>
    public int IcsSequence { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
