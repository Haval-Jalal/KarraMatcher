using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser och skriver klubbens hemmaplan (`#307`). Hemmaplanen bor på klubben och delas av alla
/// dess truppar, men nås via en trupp (adressen är trupp-scopad, behörigheten är <c>AdminOfTrupp</c>).
/// </summary>
public interface IClubVenueRepository
{
    /// <summary>Klubben en trupp hör till, spårad för uppdatering. Null när truppen inte finns.</summary>
    public Task<Club?> FindClubByTruppAsync(Guid truppId, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
