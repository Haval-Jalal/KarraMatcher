using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;

namespace KarraMatcher.Application.Tests;

/// <summary>Roller i minnet. Tomma om inget annat sagts — en vanlig förälder.</summary>
internal sealed class FakeRoleRepository(AccountRoles? roles = null) : IRoleRepository
{
    public Task<AccountRoles> GetRolesAsync(Guid accountId, CancellationToken cancellationToken) =>
        Task.FromResult(roles ?? AccountRoles.None);
}
