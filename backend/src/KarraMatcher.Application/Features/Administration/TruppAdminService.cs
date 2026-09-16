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
    public async Task<AdminResult<TruppDto>> CreateAsync(
        Guid clubId,
        Guid sportId,
        string name,
        string season,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(season);

        name = name.Trim();
        season = season.Trim();

        if (!await repository.ClubExistsAsync(clubId, cancellationToken).ConfigureAwait(false)
            || !await repository.SportExistsAsync(sportId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<TruppDto>();
        }

        if (await repository.TruppNameTakenAsync(clubId, name, season, null, cancellationToken)
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
            Season = season,
        };

        await repository.AddTruppAsync(trupp, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.TruppCreated, actorAccountId, cancellationToken, trupp.Id,
            $"{name} {season}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Läses om med klubb och sport inlästa, så svaret kan visa deras namn.
        var reloaded = await repository.FindTruppAsync(trupp.Id, cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(reloaded!.ToDto());
    }

    public async Task<AdminResult<TruppDto>> UpdateAsync(
        Guid id,
        Guid sportId,
        string name,
        string season,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(season);

        name = name.Trim();
        season = season.Trim();

        var trupp = await repository.FindTruppAsync(id, cancellationToken).ConfigureAwait(false);

        if (trupp is null)
        {
            return AdminResults.NotFound<TruppDto>();
        }

        if (!await repository.SportExistsAsync(sportId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<TruppDto>();
        }

        if (await repository.TruppNameTakenAsync(trupp.ClubId, name, season, id, cancellationToken)
            .ConfigureAwait(false))
        {
            return AdminResults.Conflict<TruppDto>();
        }

        var before = $"{trupp.Name} {trupp.Season}";

        trupp.SportId = sportId;
        trupp.Name = name;
        trupp.Season = season;

        await audit.RecordAsync(
            AuditActions.TruppUpdated, actorAccountId, cancellationToken, trupp.Id,
            $"{before} -> {name} {season}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var reloaded = await repository.FindTruppAsync(trupp.Id, cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(reloaded!.ToDto());
    }
}
