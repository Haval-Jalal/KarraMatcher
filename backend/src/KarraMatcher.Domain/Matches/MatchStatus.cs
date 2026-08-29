namespace KarraMatcher.Domain.Matches;

public enum MatchStatus
{
    /// <summary>Spelas som planerat.</summary>
    Scheduled = 0,

    /// <summary>Inställd. Visar ingen resultatinmatning och blir CANCELLED i ICS-feeden.</summary>
    Cancelled = 1,

    /// <summary>Framflyttad utan nytt datum ännu.</summary>
    Postponed = 2,
}
