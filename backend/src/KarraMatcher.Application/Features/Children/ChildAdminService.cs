using System.Globalization;

using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Application.Features.Consent;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Children;

namespace KarraMatcher.Application.Features.Children;

/// <summary>
/// Adminens barnhantering (§KM.1, `#196`): skapa barn i en trupp, sortera i lag, koppla
/// vårdnadshavare.
///
/// <para>
/// Barnprofilen är minimal (förnamn + initial). <b>Att koppla en vårdnadshavare kräver att
/// den vårdnadshavaren gett aktuellt samtycke</b> (§KM.6, via <see cref="ConsentService"/>) —
/// det är där barnprofilens rättsliga grund vilar. Barn kan skapas som roster-poster innan
/// någon vårdnadshavare kopplats. Audit loggar id, aldrig barnets namn (§KM.10).
/// </para>
/// </summary>
public sealed class ChildAdminService(
    IChildRepository children,
    IAuditLog audit,
    ConsentService consent,
    TimeProvider clock)
{
    public async Task<AdminResult<ChildDto>> CreateAsync(
        Guid ageGroupId,
        string firstName,
        string lastInitial,
        Guid? teamId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(firstName);
        ArgumentNullException.ThrowIfNull(lastInitial);

        if (!await children.TruppExistsAsync(ageGroupId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.NotFound<ChildDto>();
        }

        if (teamId is not null
            && !await children.TeamInTruppAsync(teamId.Value, ageGroupId, cancellationToken)
                .ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<ChildDto>();
        }

        var child = new Child
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroupId,
            TeamId = teamId,
            FirstName = firstName.Trim(),
            LastInitial = lastInitial.Trim(),
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        };

        await children.AddAsync(child, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(AuditActions.ChildCreated, actorAccountId, cancellationToken, child.Id)
            .ConfigureAwait(false);
        await children.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(await ReloadAsync(child.Id, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AdminResult<ChildDto>> UpdateAsync(
        Guid ageGroupId,
        Guid id,
        string firstName,
        string lastInitial,
        Guid? teamId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(firstName);
        ArgumentNullException.ThrowIfNull(lastInitial);

        var child = await children.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (child is null || child.AgeGroupId != ageGroupId)
        {
            return AdminResults.NotFound<ChildDto>();
        }

        if (teamId is not null
            && !await children.TeamInTruppAsync(teamId.Value, ageGroupId, cancellationToken)
                .ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<ChildDto>();
        }

        child.FirstName = firstName.Trim();
        child.LastInitial = lastInitial.Trim();
        child.TeamId = teamId;

        await audit.RecordAsync(AuditActions.ChildUpdated, actorAccountId, cancellationToken, child.Id)
            .ConfigureAwait(false);
        await children.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(await ReloadAsync(child.Id, cancellationToken).ConfigureAwait(false));
    }

    public async Task<AdminOutcome> DeleteAsync(
        Guid ageGroupId, Guid id, Guid actorAccountId, CancellationToken cancellationToken)
    {
        var child = await children.FindByIdAsync(id, cancellationToken).ConfigureAwait(false);

        if (child is null || child.AgeGroupId != ageGroupId)
        {
            return AdminOutcome.NotFound;
        }

        // Direkt radering, inte mjuk (§KM.6). Vårdnadshavarkopplingarna försvinner med barnet
        // (cascade). Spelarkortet berörs inte — det bor på familjens enhet (§KM.2).
        children.Remove(child);
        await audit.RecordAsync(AuditActions.ChildDeleted, actorAccountId, cancellationToken, child.Id)
            .ConfigureAwait(false);
        await children.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }

    public async Task<LinkGuardianOutcome> LinkGuardianAsync(
        Guid ageGroupId,
        Guid childId,
        string email,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var child = await children.FindByIdAsync(childId, cancellationToken).ConfigureAwait(false);

        if (child is null || child.AgeGroupId != ageGroupId)
        {
            return LinkGuardianOutcome.ChildNotFound;
        }

        var account = await children.FindAccountByEmailAsync(email, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return LinkGuardianOutcome.AccountNotFound;
        }

        if (!await children.HasJoinedTruppAsync(account.Id, ageGroupId, cancellationToken)
            .ConfigureAwait(false))
        {
            return LinkGuardianOutcome.NotTruppMember;
        }

        // §KM.6: vårdnadshavaren måste ha gett aktuellt samtycke innan barnet kopplas.
        if (!await consent.HasCurrentConsentAsync(account.Id, cancellationToken).ConfigureAwait(false))
        {
            return LinkGuardianOutcome.NoConsent;
        }

        if (await children.GuardianshipExistsAsync(account.Id, childId, cancellationToken)
            .ConfigureAwait(false))
        {
            return LinkGuardianOutcome.AlreadyLinked;
        }

        await children.AddGuardianshipAsync(
            new Guardianship
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,
                ChildId = childId,
                GrantedUtc = clock.GetUtcNow().UtcDateTime,
            },
            cancellationToken).ConfigureAwait(false);

        await audit.RecordAsync(
            AuditActions.GuardianLinked, actorAccountId, cancellationToken, childId,
            $"konto:{account.Id.ToString("D", CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await children.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return LinkGuardianOutcome.Linked;
    }

    public async Task<AdminOutcome> UnlinkGuardianAsync(
        Guid ageGroupId,
        Guid childId,
        Guid accountId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var child = await children.FindByIdAsync(childId, cancellationToken).ConfigureAwait(false);

        if (child is null || child.AgeGroupId != ageGroupId)
        {
            return AdminOutcome.NotFound;
        }

        var guardianship = await children.FindGuardianshipAsync(accountId, childId, cancellationToken)
            .ConfigureAwait(false);

        if (guardianship is null)
        {
            return AdminOutcome.NotFound;
        }

        children.RemoveGuardianship(guardianship);
        await audit.RecordAsync(
            AuditActions.GuardianUnlinked, actorAccountId, cancellationToken, childId,
            $"konto:{accountId.ToString("D", CultureInfo.InvariantCulture)}").ConfigureAwait(false);
        await children.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminOutcome.Success;
    }

    /// <summary>Läser om barnet med laget inläst; nyskapade/ändrade barn har inga guardians i svaret.</summary>
    private async Task<ChildDto> ReloadAsync(Guid childId, CancellationToken cancellationToken)
    {
        var saved = await children.FindByIdAsync(childId, cancellationToken).ConfigureAwait(false);

        return saved!.ToDto([]);
    }
}
