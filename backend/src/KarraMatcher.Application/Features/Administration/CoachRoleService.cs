using System.Globalization;

using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Adminens tillsättning av tränare per lag (§KM.3, `#197`).
///
/// <para>
/// En tränare tilldelas ett <em>befintligt konto</em> via dess adress; en person utan konto
/// bjuds in i inbjudningsflödet (`#193`) först. Rollen skrivs som en
/// <c>TeamRole{Coach, TeamId}</c>, och läs-sidan gör den till ett <c>coach</c>-anspråk
/// (lagets slug) automatiskt — därmed når tränaren sitt lag via <c>CoachOfTeam</c>.
/// </para>
///
/// <para>
/// <b>Objektnivå-auktorisering:</b> både tillsättning och avsättning kräver att laget hör till
/// truppen i adressen. En admin för en trupp kan alltså inte tillsätta eller avsätta en tränare
/// i en annans lag genom att gissa ett lag-id — laget kontrolleras höra hemma här (§KM.3).
/// Audit-loggas med kontots id, aldrig dess adress (§KM.10).
/// </para>
/// </summary>
public sealed class CoachRoleService(IAdministrationRepository repository, IAuditLog audit)
{
    public async Task<AdminResult<TeamCoachDto>> GrantAsync(
        Guid truppId, Guid teamId, string email, Guid actorAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        // Laget måste finnas och höra till truppen i adressen — annars 404 (inte en annans lag).
        var team = await repository.FindLagAsync(teamId, cancellationToken).ConfigureAwait(false);

        if (team is null || team.AgeGroupId != truppId)
        {
            return AdminResults.NotFound<TeamCoachDto>();
        }

        var account = await repository.FindAccountByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        // Inget konto: personen bjuds in i #193 i stället. 400, inte 404 — laget finns.
        if (account is null)
        {
            return AdminResults.ReferenceMissing<TeamCoachDto>();
        }

        var existing = await repository.FindCoachRoleAsync(account.Id, teamId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            return AdminResults.Conflict<TeamCoachDto>();
        }

        var role = new TeamRole
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            TeamId = teamId,
            Role = RoleKind.Coach,
            GrantedUtc = DateTime.UtcNow,
            Account = account,
        };

        await repository.AddRoleAsync(role, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.CoachGranted, actorAccountId, cancellationToken, account.Id,
            $"lag:{teamId.ToString("D", CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(role.ToCoachDto());
    }

    public async Task<AdminOutcome> RevokeAsync(
        Guid truppId, Guid teamId, Guid accountId, Guid actorAccountId, CancellationToken cancellationToken)
    {
        var team = await repository.FindLagAsync(teamId, cancellationToken).ConfigureAwait(false);

        if (team is null || team.AgeGroupId != truppId)
        {
            return AdminOutcome.NotFound;
        }

        var role = await repository.FindCoachRoleAsync(accountId, teamId, cancellationToken)
            .ConfigureAwait(false);

        if (role is null)
        {
            return AdminOutcome.NotFound;
        }

        repository.RemoveRole(role);
        await audit.RecordAsync(
            AuditActions.CoachRevoked, actorAccountId, cancellationToken, accountId,
            $"lag:{teamId.ToString("D", CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }
}
