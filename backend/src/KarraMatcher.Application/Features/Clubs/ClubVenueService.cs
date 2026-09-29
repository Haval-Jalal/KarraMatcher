using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Clubs;

/// <summary>Truppens hemmaplan så FE:t visar den (`#307`/`#405`). <c>Configured</c> = en plan är satt.</summary>
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

    /// <summary>Truppen finns inte.</summary>
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
/// Truppens hemmaplan (`#307`/`#405`): den plan truppen spelar hemma på. En admin för truppen
/// skriver in namn + adress; adressen geokodas (samma väg som det gamla registret) så att väder
/// och vägbeskrivning har koordinater. Planen hör till truppen — varje trupp sätter sin egen och
/// en trupp-admin når aldrig en annan trupps plan (isolering). (Typnamnet är historiskt "Club".)
/// </summary>
public sealed class ClubVenueService(IClubVenueRepository trupper, IGeocoder geocoder, IAuditLog audit)
{
    public async Task<ClubVenueDto?> GetByTruppAsync(Guid truppId, CancellationToken cancellationToken)
    {
        var trupp = await trupper.FindTruppAsync(truppId, cancellationToken).ConfigureAwait(false);

        if (trupp is null)
        {
            return null;
        }

        return new ClubVenueDto(
            trupp.HomeVenueName,
            trupp.HomeAddress,
            trupp.HomeLatitude,
            trupp.HomeLongitude,
            trupp.HomeLatitude is not null && trupp.HomeLongitude is not null);
    }

    /// <summary>Sätter (eller ändrar) truppens hemmaplan. Adressen geokodas till koordinater.</summary>
    public async Task<SetClubVenueResult> SetByTruppAsync(
        Guid truppId,
        string name,
        string address,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var trupp = await trupper.FindTruppAsync(truppId, cancellationToken).ConfigureAwait(false);

        if (trupp is null)
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

        trupp.HomeVenueName = name?.Trim() ?? string.Empty;
        trupp.HomeAddress = place.Label;
        trupp.HomeLatitude = place.Latitude;
        trupp.HomeLongitude = place.Longitude;

        // Audit-målet är truppen (planen hör till den), inte klubben.
        await audit.RecordAsync(
            AuditActions.ClubHomeVenueSet, actorAccountId, cancellationToken, trupp.Id)
            .ConfigureAwait(false);

        await trupper.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new SetClubVenueResult(SetClubVenueOutcome.Set, []);
    }
}
