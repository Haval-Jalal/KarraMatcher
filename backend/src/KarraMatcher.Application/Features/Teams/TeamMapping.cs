using KarraMatcher.Application.Features.Events;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Teams;

/// <summary>
/// Enda stället där entiteter blir DTO:er. Att hålla mappningen samlad gör det svårt att
/// råka exponera ett fält som inte hör hemma i ett svar (§KM.3).
/// </summary>
internal static class TeamMapping
{
    public static TeamDto ToDto(this Team team) => new(
        team.Slug,
        team.Name,
        team.AgeGroup?.Name ?? string.Empty,
        team.ColorHex);

    public static EventDto ToDto(this Event item) => new(
        item.Id,
        item.Type.ToString(),
        new DateTimeOffset(item.KickoffUtc, TimeSpan.Zero),
        item.Title,
        item.OpponentName,
        item.IsHome,
        item.Status.ToString(),

        // Avvikande adress vinner över spelplatsens. Tom sträng räknas som "ingen
        // avvikelse" -- annars hade en tom textruta i tränarvyn raderat adressen.
        string.IsNullOrWhiteSpace(item.AddressOverride)
            ? item.Venue?.Address ?? string.Empty
            : item.AddressOverride,

        new VenueDto(
            item.Venue?.Name ?? string.Empty,
            item.Venue?.Address ?? string.Empty,
            item.Venue?.Latitude ?? 0,
            item.Venue?.Longitude ?? 0));
}
