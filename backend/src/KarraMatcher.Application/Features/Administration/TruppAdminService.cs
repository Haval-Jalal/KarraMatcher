using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Superadmins hantering av trupper (kodnamn <c>AgeGroup</c>) (§KM.3, `#192`).
///
/// <para>
/// En trupp binds till en klubb och en sport och identifieras av klubb + namn + säsong —
/// ett unikt DB-villkor bevakar det, och tjänsten prövar det i förväg för att ge ett
/// begripligt <c>409</c> i stället för ett databasfel. Audit-loggas (§KM.10).
/// </para>
/// </summary>
public sealed class TruppAdminService(IAdministrationRepository repository, IAuditLog audit)
{
    // Säsongen utgick (`#261`): en trupp identifieras nu av klubb + namn. Kolumnen finns kvar
    // men lämnas tom för nya trupper, så det unika villkoret blir i praktiken (klubb, namn).
    private const string NoSeason = "";

    public async Task<AdminResult<TruppDto>> CreateAsync(
        Guid clubId,
        Guid sportId,
        string name,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        name = name.Trim();

        if (!await repository.ClubExistsAsync(clubId, cancellationToken).ConfigureAwait(false)
            || !await repository.SportExistsAsync(sportId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<TruppDto>();
        }

        if (await repository.TruppNameTakenAsync(clubId, name, NoSeason, null, cancellationToken)
            .ConfigureAwait(false))
        {
            return AdminResults.Conflict<TruppDto>();
        }

        var trupp = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = clubId,
            SportId = sportId,
            Name = name,
            Season = NoSeason,
        };

        await repository.AddTruppAsync(trupp, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.TruppCreated, actorAccountId, cancellationToken, trupp.Id,
            name).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Läses om med klubb och sport inlästa, så svaret kan visa deras namn.
        var reloaded = await repository.FindTruppAsync(trupp.Id, cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(reloaded!.ToDto());
    }

    public async Task<AdminResult<TruppDto>> UpdateAsync(
        Guid id,
        Guid sportId,
        string name,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        name = name.Trim();

        var trupp = await repository.FindTruppAsync(id, cancellationToken).ConfigureAwait(false);

        if (trupp is null)
        {
            return AdminResults.NotFound<TruppDto>();
        }

        if (!await repository.SportExistsAsync(sportId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<TruppDto>();
        }

        // Säsongen (`#261`) rörs inte längre vid ändring; den befintliga behålls i unikhetskollen.
        if (await repository
            .TruppNameTakenAsync(trupp.ClubId, name, trupp.Season, id, cancellationToken)
            .ConfigureAwait(false))
        {
            return AdminResults.Conflict<TruppDto>();
        }

        var before = trupp.Name;

        trupp.SportId = sportId;
        trupp.Name = name;

        await audit.RecordAsync(
            AuditActions.TruppUpdated, actorAccountId, cancellationToken, trupp.Id,
            $"{before} -> {name}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var reloaded = await repository.FindTruppAsync(trupp.Id, cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(reloaded!.ToDto());
    }
}
