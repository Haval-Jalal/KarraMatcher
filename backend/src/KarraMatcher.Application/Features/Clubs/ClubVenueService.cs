using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Clubs;

/// <summary>Klubbens hemmaplan så FE:t visar den (`#307`). <c>Configured</c> = en plan är satt.</summary>
public sealed record ClubVenueDto(
    string? Name,
    string? Address,
    double? Latitude,
    double? Longitude,
    bool Configured);

/// <summary>Vad ett försök att sätta klubbens hemmaplan slutade med (`#307`).</summary>
public enum SetClubVenueOutcome
{
    /// <summary>Hemmaplanen sattes.</summary>
    Set = 0,

    /// <summary>Truppen (och därmed klubben) finns inte.</summary>
    TruppNotFound = 1,

    /// <summary>Adressen gick inte att hitta vid geokodningen.</summary>
    AddressNotFound = 2,

    /// <summary>Adressen matchade flera platser — tränaren får välja.</summary>
    Ambiguous = 3,
}

/// <summary>Utfallet av att sätta hemmaplanen, med kandidater vid flertydig adress.</summary>
public sealed record SetClubVenueResult(
    SetClubVenueOutcome Outcome,
    IReadOnlyList<GeocodedPlace> Candidates);

/// <summary>
/// Klubbens hemmaplan (`#307`): den plan alla klubbens truppar spelar hemma på. En tränare i
/// klubben skriver in namn + adress; adressen geokodas (samma väg som det gamla registret) så
/// att väder och vägbeskrivning har koordinater. Planen bor på klubben och delas — sätts den
/// via en trupp gäller den alla truppar i samma klubb.
/// </summary>
public sealed class ClubVenueService(IClubVenueRepository clubs, IGeocoder geocoder, IAuditLog audit)
{
    public async Task<ClubVenueDto?> GetByTruppAsync(Guid truppId, CancellationToken cancellationToken)
    {
        var club = await clubs.FindClubByTruppAsync(truppId, cancellationToken).ConfigureAwait(false);

        if (club is null)
        {
            return null;
        }

        return new ClubVenueDto(
            club.HomeVenueName,
            club.HomeAddress,
            club.HomeLatitude,
            club.HomeLongitude,
            club.HomeLatitude is not null && club.HomeLongitude is not null);
    }

    /// <summary>Sätter (eller ändrar) klubbens hemmaplan. Adressen geokodas till koordinater.</summary>
    public async Task<SetClubVenueResult> SetByTruppAsync(
        Guid truppId,
        string name,
        string address,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var club = await clubs.FindClubByTruppAsync(truppId, cancellationToken).ConfigureAwait(false);

        if (club is null)
        {
            return new SetClubVenueResult(SetClubVenueOutcome.TruppNotFound, []);
        }

        var hits = await geocoder.LookupAsync(address?.Trim() ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (hits.Count == 0)
        {
            return new SetClubVenueResult(SetClubVenueOutcome.AddressNotFound, []);
        }

        if (hits.Count > 1)
        {
            // Aldrig gissa (som venue-registret): flera "Idrottsvägen" ger fel plan. Tränaren
            // väljer och skickar tillbaka den valda adressen, så uppslaget blir entydigt.
            return new SetClubVenueResult(SetClubVenueOutcome.Ambiguous, hits);
        }

        var place = hits[0];

        club.HomeVenueName = name?.Trim() ?? string.Empty;
        club.HomeAddress = place.Label;
        club.HomeLatitude = place.Latitude;
        club.HomeLongitude = place.Longitude;

        await audit.RecordAsync(
            AuditActions.ClubHomeVenueSet, actorAccountId, cancellationToken, club.Id)
            .ConfigureAwait(false);

        await clubs.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SetClubVenueResult(SetClubVenueOutcome.Set, []);
    }
}
