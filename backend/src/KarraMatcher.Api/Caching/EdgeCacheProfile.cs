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
/// Markerar en endpoint som publikt cachebar.
///
/// <para>
/// Sätts som attribut på en controller-action, eller via
/// <see cref="EdgeCache.WithEdgeCache"/> på en minimal-API-endpoint. Båda vägarna hamnar
/// som endpoint-metadata, vilket är det mellanvaran läser — därför räcker en typ för båda.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class EdgeCacheAttribute(EdgeCacheProfile profile) : Attribute
{
    /// <summary>Innehållstypen som avgör livslängden.</summary>
    public EdgeCacheProfile Profile { get; } = profile;
}
