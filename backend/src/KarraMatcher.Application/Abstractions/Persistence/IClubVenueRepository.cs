using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser och skriver truppens hemmaplan (`#405`). Hemmaplanen hör till truppen (inte klubben) —
/// varje trupp sätter sin egen och en trupp-admin når aldrig en annan trupps plan (isolering).
/// (Typnamnet är historiskt "ClubVenue" — planen låg tidigare på klubben.)
/// </summary>
public interface IClubVenueRepository
{
    /// <summary>Truppen, spårad för uppdatering. Null när truppen inte finns.</summary>
    public Task<AgeGroup?> FindTruppAsync(Guid truppId, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
