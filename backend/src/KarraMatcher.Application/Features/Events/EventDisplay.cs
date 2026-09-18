using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Events;

/// <summary>
/// En kort etikett för en händelse, delad av notiser och audit (`#198`).
///
/// <para>
/// En match beskrivs av hemma/borta och motståndare; en träning eller övrig händelse av sin
/// rubrik. Att hålla det på ett ställe gör att notistext och audit-rad aldrig glider isär.
/// Aldrig fritext eller barn-PII här (§KM.1).
/// </para>
/// </summary>
internal static class EventDisplay
{
    /// <summary>Etiketten, byggd ur DTO-fälten (Type är en sträng i DTO:n).</summary>
    public static string Label(string type, bool? isHome, string? opponent, string? title) =>
        type == nameof(EventType.Match)
            ? $"{Side(isHome)} mot {opponent}"
            : title ?? string.Empty;

    /// <summary>Etiketten, byggd ur domänvärden (för reminder-projektionen).</summary>
    public static string Label(EventType type, bool? isHome, string? opponent, string? title) =>
        Label(type.ToString(), isHome, opponent, title);

    private static string Side(bool? isHome) => isHome == true ? "hemma" : "borta";
}
