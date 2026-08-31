using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class RoleRepository(KarraMatcherDbContext context) : IRoleRepository
{
    public async Task<AccountRoles> GetRolesAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var rows = await context.TeamRoles
            .AsNoTracking()
            .Where(r => r.AccountId == accountId)
            .Select(r => new { r.Role, Slug = r.Team!.Slug })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var isAdmin = rows.Exists(r => r.Role == RoleKind.Admin);

        var coachOf = rows
            .Where(r => r.Role == RoleKind.Coach && r.Slug != null)
            .Select(r => r.Slug!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToArray();

        return new AccountRoles(isAdmin, coachOf);
    }
}
