using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class RefreshTokenRepository(KarraMatcherDbContext context) : IRefreshTokenRepository
{
    /// <summary>
    /// Spårad uppslagning med kontot laddat.
    ///
    /// <para>
    /// Medvetet <em>inte</em> <c>AsNoTracking</c>, till skillnad från läsvägarna i appen:
    /// den token som hittas ska märkas som ersatt i samma enhet av arbete.
    /// </para>
    /// </summary>
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        context.RefreshTokens
            .Include(t => t.Account)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken) =>
        await context.RefreshTokens.AddAsync(token, cancellationToken).ConfigureAwait(false);

    public async Task RevokeFamilyAsync(
        Guid familyId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        // Laddas och sätts i minnet i stället för med ExecuteUpdate, eftersom
        // in-memory-providern som integrationstesterna använder inte stödjer det senare.
        // Familjerna är små -- en handfull rader per session -- så skillnaden är teoretisk.
        var family = await context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in family)
        {
            token.RevokedUtc = nowUtc;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
