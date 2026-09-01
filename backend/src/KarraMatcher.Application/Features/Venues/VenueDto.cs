namespace KarraMatcher.Application.Features.Venues;

/// <summary>En spelplats i registret.</summary>
public sealed record VenueDto(
    Guid Id,
    string Name,
    string Address,
    double Latitude,
    double Longitude,
    bool IsHome);

internal static class VenueMapping
{
    public static VenueDto ToDto(this Domain.Matches.Venue venue) => new(
        venue.Id,
        venue.Name,
        venue.Address,
        venue.Latitude,
        venue.Longitude,
        venue.IsHome);
}
