using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Children;

/// <summary>Lagets id och dess trupp (AgeGroup), för att slå upp ett färg-lag via dess slug.</summary>
public sealed record TeamRefDto(Guid TeamId, Guid TruppId);

/// <summary>
/// Slår upp ett färg-lags id + trupp ur dess slug (`#redesign`). Används av tränarens
/// kallelse-endpoints för att gå från adressens slug till (lag, trupp) innan kallelsen ställs.
/// </summary>
public sealed record GetTeamRefBySlugQuery(string Slug) : IQuery<TeamRefDto?>;

// Slugen kommer från URL:en. Utan validator hoppas kontrollen tyst över (samma mönster som #551);
// samma grammatik + längdtak som GetTeamEventsQueryValidator (#586).
internal sealed class GetTeamRefBySlugQueryValidator : AbstractValidator<GetTeamRefBySlugQuery>
{
    public GetTeamRefBySlugQueryValidator()
    {
        RuleFor(query => query.Slug)
            .NotEmpty().WithMessage("Laget måste anges.")
            .MaximumLength(80).WithMessage("Lagnamnet är för långt.")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Laget kan bara innehålla små bokstäver, siffror och bindestreck.");
    }
}

internal sealed class GetTeamRefBySlugQueryHandler(IChildRepository children)
    : IQueryHandler<GetTeamRefBySlugQuery, TeamRefDto?>
{
    public async Task<TeamRefDto?> HandleAsync(
        GetTeamRefBySlugQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var team = await children.FindTeamBySlugAsync(query.Slug, cancellationToken)
            .ConfigureAwait(false);

        return team is null ? null : new TeamRefDto(team.Id, team.AgeGroupId);
    }
}
