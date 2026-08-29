using KarraMatcher.Application.Features.Matches;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Teams;

/// <summary>
/// Enda stället där entiteter blir DTO:er. Att hålla mappningen samlad gör det svårt att
/// råka exponera ett fält som inte hör hemma i ett publikt svar (§KM.3).
/// </summary>
internal static class TeamMapping
{
    public static TeamDto ToDto(this Team team) => new(
        team.Slug,
        team.Name,
        team.AgeGroup?.Name ?? string.Empty,
        team.ColorHex);

    public static MatchDto ToDto(this Match match) => new(
        match.Id,
        new DateTimeOffset(match.KickoffUtc, TimeSpan.Zero),
        match.OpponentName,
        match.IsHome,
        match.Status.ToString(),

        // Avvikande adress vinner över spelplatsens. Tom sträng räknas som "ingen
        // avvikelse" -- annars hade en tom textruta i tränarvyn raderat adressen.
        string.IsNullOrWhiteSpace(match.AddressOverride)
            ? match.Venue?.Address ?? string.Empty
            : match.AddressOverride,

        new VenueDto(
            match.Venue?.Name ?? string.Empty,
            match.Venue?.Address ?? string.Empty,
            match.Venue?.Latitude ?? 0,
            match.Venue?.Longitude ?? 0));
}
