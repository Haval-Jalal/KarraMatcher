using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Superadmins hantering av sporter (§KM.3, `#192`).
///
/// <para>
/// Slugen sätts vid skapande och ändras aldrig: den är den stabila identifieraren i URL:er
/// och delade länkar. Ett namn får däremot rättas. Varje åtgärd audit-loggas (§KM.10) — men
/// bara namn och slug, som inte är personuppgifter.
/// </para>
/// </summary>
public sealed class SportAdminService(IAdministrationRepository repository, IAuditLog audit)
{
    public async Task<AdminResult<SportDto>> CreateAsync(
        string name, string slug, Guid actorAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(slug);

        name = name.Trim();
        slug = slug.Trim();

        if (await repository.SportSlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.Conflict<SportDto>();
        }

        var sport = new Sport { Id = Guid.NewGuid(), Name = name, Slug = slug };

        await repository.AddSportAsync(sport, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.SportCreated, actorAccountId, cancellationToken, sport.Id, $"{name} ({slug})")
            .ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(sport.ToDto());
    }

    public async Task<AdminResult<SportDto>> UpdateAsync(
        Guid id, string name, Guid actorAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        name = name.Trim();

        var sport = await repository.FindSportAsync(id, cancellationToken).ConfigureAwait(false);

        if (sport is null)
        {
            return AdminResults.NotFound<SportDto>();
        }

        var before = sport.Name;
        sport.Name = name;

        await audit.RecordAsync(
            AuditActions.SportUpdated, actorAccountId, cancellationToken, sport.Id,
            $"namn: {before} -> {name}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(sport.ToDto());
    }
}
