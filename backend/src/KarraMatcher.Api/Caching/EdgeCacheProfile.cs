namespace KarraMatcher.Api.Caching;

/// <summary>
/// Vilken sorts publikt innehåll en endpoint svarar med. Profilen — inte ett antal
/// sekunder — är det en endpoint anger, så att alla scheman får samma livslängd och den
/// kan ändras på ett ställe.
/// </summary>
public enum EdgeCacheProfile
{
    /// <summary>Lagets matchlista.</summary>
    Schedule = 1,

    /// <summary>Enskild match.</summary>
    MatchDetail = 2,

    /// <summary>ICS-kalenderfeeden.</summary>
    Calendar = 3,

    /// <summary>Spelplatser och annat som i praktiken aldrig ändras.</summary>
    Reference = 4,
}

/// <summary>
/// Markerar en endpoint som publikt cachebar. Läggs på som endpoint-metadata via
/// <see cref="EdgeCache.WithEdgeCache"/> och läses av mellanvaran.
/// </summary>
/// <param name="Profile">Innehållstypen som avgör livslängden.</param>
public sealed record EdgeCacheMetadata(EdgeCacheProfile Profile);
