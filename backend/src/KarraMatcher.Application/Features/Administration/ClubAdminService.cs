using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Superadmins hantering av klubbar (§KM.3, `#192`). Slugen är stabil och ändras inte;
/// namnet får rättas. Audit-loggas (§KM.10).
/// </summary>
public sealed class ClubAdminService(IAdministrationRepository repository, IAuditLog audit)
{
    public async Task<AdminResult<ClubDto>> CreateAsync(
        string name, string slug, Guid actorAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(slug);

        name = name.Trim();
        slug = slug.Trim();

        if (await repository.ClubSlugExistsAsync(slug, cancellationToken).ConfigureAwait(false))
        {
            return AdminResults.Conflict<ClubDto>();
        }

        var club = new Club { Id = Guid.NewGuid(), Name = name, Slug = slug };

        await repository.AddClubAsync(club, cancellationToken).ConfigureAwait(false);
        await audit.RecordAsync(
            AuditActions.ClubCreated, actorAccountId, cancellationToken, club.Id, $"{name} ({slug})")
            .ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(club.ToDto());
    }

    public async Task<AdminResult<ClubDto>> UpdateAsync(
        Guid id, string name, Guid actorAccountId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);

        name = name.Trim();

        var club = await repository.FindClubAsync(id, cancellationToken).ConfigureAwait(false);

        if (club is null)
        {
            return AdminResults.NotFound<ClubDto>();
        }

        var before = club.Name;
        club.Name = name;

        await audit.RecordAsync(
            AuditActions.ClubUpdated, actorAccountId, cancellationToken, club.Id,
            $"namn: {before} -> {name}").ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return AdminResults.Ok(club.ToDto());
    }
}
