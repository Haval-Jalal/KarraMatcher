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
            .Select(r => new { r.Role, TeamSlug = r.Team!.Slug, r.AgeGroupId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var isSuperAdmin = rows.Exists(r => r.Role == RoleKind.SuperAdmin);

        var adminOf = rows
            .Where(r => r.Role == RoleKind.Admin && r.AgeGroupId != null)
            .Select(r => r.AgeGroupId!.Value.ToString())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var coachOf = rows
            .Where(r => r.Role == RoleKind.Coach && r.TeamSlug != null)
            .Select(r => r.TeamSlug!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToArray();

        return new AccountRoles(isSuperAdmin, adminOf, coachOf);
    }
}
