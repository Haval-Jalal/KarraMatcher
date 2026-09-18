namespace KarraMatcher.Domain.Events;

public enum EventStatus
{
    /// <summary>Äger rum som planerat.</summary>
    Scheduled = 0,

    /// <summary>Inställd. Visar ingen resultatinmatning.</summary>
    Cancelled = 1,

    /// <summary>Framflyttad utan nytt datum ännu.</summary>
    Postponed = 2,
}
