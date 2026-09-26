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

    public static EventDto ToDto(this Event item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var (name, address, latitude, longitude) = ResolveLocation(item);

        // Visnings-adressen: en avvikande adress vinner över en <b>legacy</b>-spelplats (den gamla
        // AddressOverride-regeln), men VenueDto:n behåller alltid platsens egen adress. Utanför
        // legacy (hemma/borta) är de samma (`#307`).
        var displayAddress = item.Venue is not null && !string.IsNullOrWhiteSpace(item.AddressOverride)
            ? item.AddressOverride!
            : address;

        return new EventDto(
            item.Id,
            item.Type.ToString(),
            new DateTimeOffset(item.KickoffUtc, TimeSpan.Zero),
            item.Title,
            item.OpponentName,
            item.IsHome,
            item.Status.ToString(),
            displayAddress,
            new VenueDto(name, address, latitude, longitude));
    }

    /// <summary>
    /// Händelsens plats: namn, adress och koordinat (`#307`). Tre vägar:
    /// <list type="number">
    /// <item>äldre rader pekar på spelplatsregistret (seed) — då gäller <see cref="Event.Venue"/>;</item>
    /// <item>en hemma-händelse löser platsen ur klubbens hemmaplan;</item>
    /// <item>annars (borta/annan plats) ur händelsens egen adress + koordinat.</item>
    /// </list>
    /// </summary>
    private static (string Name, string Address, double Latitude, double Longitude) ResolveLocation(
        Event item)
    {
        if (item.Venue is not null)
        {
            // Spelplatsens egen adress/koordinat. Den ev. avvikande adressen hanteras i ToDto.
            return (item.Venue.Name, item.Venue.Address, item.Venue.Latitude, item.Venue.Longitude);
        }

        if (item.IsHome == true)
        {
            var club = item.Team?.AgeGroup?.Club;

            return (
                club?.HomeVenueName ?? string.Empty,
                club?.HomeAddress ?? string.Empty,
                club?.HomeLatitude ?? 0,
                club?.HomeLongitude ?? 0);
        }

        // Borta/annan plats: händelsens egen adress. Namnet lämnas tomt — adressen bär platsen.
        return (string.Empty, item.AddressOverride ?? string.Empty, item.Latitude ?? 0, item.Longitude ?? 0);
    }
}
