using System.Globalization;

using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Superadmins tillsättning av admins per trupp (§KM.3, `#192`).
///
/// <para>
/// En admin tilldelas ett <em>befintligt konto</em> via dess adress; en person som inte har
/// ett konto ännu bjuds in i inbjudningsflödet (`#193`). Rollen skrivs som en
/// <c>TeamRole{Admin, AgeGroupId}</c>, och läs-sidan gör den till ett <c>admin-trupp</c>-
/// anspråk automatiskt. Audit-loggas med kontots id, aldrig dess adress (§KM.10).
/// </para>
/// </summary>
public sealed class AdminRoleService(IAdministrationRepository repository, IAuditLog audit)
{
    public async Task<AdminResult<TruppAdminDto>> GrantAsync(
        Guid truppId, string email, Guid actorAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        if (!await repository.TruppExistsAsync(truppId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.NotFound<TruppAdminDto>();
        }

        var account = await repository.FindAccountByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        // Inget konto: personen bjuds in i #193 i stället. 400, inte 404 — truppen finns.
        if (account is null)
        {
            return AdminResults.ReferenceMissing<TruppAdminDto>();
        }

        var existing = await repository.FindAdminRoleAsync(account.Id, truppId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return AdminResults.Conflict<TruppAdminDto>();
        }

        var role = new TeamRole
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            AgeGroupId = truppId,
            Role = RoleKind.Admin,
            GrantedUtc = DateTime.UtcNow,
            Account = account,
        };

        await repository.AddRoleAsync(role, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.AdminGranted, actorAccountId, cancellationToken, account.Id,
            $"trupp:{truppId.ToString("D", CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(role.ToAdminDto());
    }

    public async Task<AdminOutcome> RevokeAsync(
        Guid truppId, Guid accountId, Guid actorAccountId, CancellationToken cancellationToken)
    {
        var role = await repository.FindAdminRoleAsync(accountId, truppId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return AdminOutcome.NotFound;
        }

        repository.RemoveRole(role);
        await audit.RecordAsync(
            AuditActions.AdminRevoked, actorAccountId, cancellationToken, accountId,
            $"trupp:{truppId.ToString("D", CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }
}
