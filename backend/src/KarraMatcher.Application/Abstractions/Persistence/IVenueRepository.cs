using KarraMatcher.Application.Features.Venues;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Abstractions.Persistence;

public interface IVenueRepository
{
    /// <summary>
    /// Spelplatser vars namn eller adress innehåller söktermen.
    ///
    /// <para>
    /// Underlaget för förslagen medan tränaren skriver. En tom sökterm ger de vanligaste
    /// först — hemmaplanerna — eftersom det är dem de flesta matcher spelas på.
    /// </para>
    /// </summary>
    public Task<IReadOnlyList<VenueDto>> SearchAsync(string term, CancellationToken cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);

    public Task AddAsync(Venue venue, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
