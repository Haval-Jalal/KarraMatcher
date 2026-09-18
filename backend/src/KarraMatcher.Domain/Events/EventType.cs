namespace KarraMatcher.Domain.Events;

/// <summary>Vad slags händelse en <see cref="Event"/> är (`#198`).</summary>
public enum EventType
{
    /// <summary>En match mot en motståndare. Har motståndare och hemma/borta.</summary>
    Match = 0,

    /// <summary>En träning. Har en rubrik i stället för motståndare.</summary>
    Training = 1,

    /// <summary>Övrig händelse (t.ex. cup, lagfest). Har en rubrik.</summary>
    Other = 2,
}
