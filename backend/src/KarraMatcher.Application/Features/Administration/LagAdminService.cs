using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Superadmins hantering av lag (kodnamn <c>Team</c>) (§KM.3, `#192`).
///
/// <para>
/// Ett lag hör till en trupp och bär lagfärgen som driver appens tema. Slugen är stabil och
/// ändras inte (den lever i delade länkar); namn och färg får rättas. Audit-loggas (§KM.10).
/// </para>
/// </summary>
public sealed class LagAdminService(IAdministrationRepository repository, IAuditLog audit)
{
    public async Task<AdminResult<LagDto>> CreateAsync(
        Guid truppId,
        string name,
        string colorHex,
        string slug,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(colorHex);
        ArgumentNullException.ThrowIfNull(slug);

        name = name.Trim();
        colorHex = colorHex.Trim();
        slug = slug.Trim();

        if (!await repository.TruppExistsAsync(truppId, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.ReferenceMissing<LagDto>();
        }

        if (await repository.LagSlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.Conflict<LagDto>();
        }

        var lag = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = truppId,
            Name = name,
            ColorHex = colorHex,
            Slug = slug,
            AttendanceEnabled = false,
        };

        await repository.AddLagAsync(lag, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.LagCreated, actorAccountId, cancellationToken, lag.Id, $"{name} ({slug})")
            .ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(lag.ToLagDto());
    }

    public async Task<AdminResult<LagDto>> UpdateAsync(
        Guid id,
        string name,
        string colorHex,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(colorHex);

        name = name.Trim();
        colorHex = colorHex.Trim();

        var lag = await repository.FindLagAsync(id, cancellationToken).ConfigureAwait(false);

        if (lag is null)
        {
            return AdminResults.NotFound<LagDto>();
        }

        var before = $"{lag.Name} {lag.ColorHex}";

        lag.Name = name;
        lag.ColorHex = colorHex;

        await audit.RecordAsync(
            AuditActions.LagUpdated, actorAccountId, cancellationToken, lag.Id,
            $"{before} -> {name} {colorHex}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(lag.ToLagDto());
    }
}
