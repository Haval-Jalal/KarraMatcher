using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class LoginCodeRepository(KarraMatcherDbContext context) : ILoginCodeRepository
{
    /// <summary>
    /// Spårad, eftersom försöksräknaren och förbrukningen skrivs på samma rad direkt
    /// efteråt. <c>AsNoTracking</c> hade krävt en andra uppslagning för att spara.
    /// </summary>
    public Task<LoginCode?> FindLatestAsync(string email, CancellationToken cancellationToken) =>
        context.LoginCodes
            .Where(c => c.Email == email)
            .OrderByDescending(c => c.CreatedUtc)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(LoginCode code, CancellationToken cancellationToken) =>
        await context.LoginCodes.AddAsync(code, cancellationToken).ConfigureAwait(false);

    public async Task ConsumeOutstandingAsync(
        string email,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var outstanding = await context.LoginCodes
            .Where(c => c.Email == email && c.ConsumedUtc == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var code in outstanding)
        {
            code.ConsumedUtc = nowUtc;
        }
    }

    public async Task DeleteForEmailAsync(string email, CancellationToken cancellationToken)
    {
        var codes = await context.LoginCodes
            .Where(c => c.Email == email)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        context.LoginCodes.RemoveRange(codes);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
