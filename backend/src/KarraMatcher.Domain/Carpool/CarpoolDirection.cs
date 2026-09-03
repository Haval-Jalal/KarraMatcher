namespace KarraMatcher.Domain.Carpool;

/// <summary>Vilken väg föraren erbjuder skjuts.</summary>
public enum CarpoolDirection
{
    /// <summary>Hemifrån till matchen.</summary>
    ToMatch = 0,

    /// <summary>Från matchen hem.</summary>
    FromMatch = 1,

    /// <summary>Båda hållen.</summary>
    Both = 2,
}
