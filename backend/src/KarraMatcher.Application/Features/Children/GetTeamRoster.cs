using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Children;

/// <summary>
/// Ett lags överblick för dess tränare (`#redesign`): lagets barn och deras vårdnadshavare.
///
/// <para>
/// Objektnivå-auktoriseringen ligger i policyn <c>CoachOfTeam</c> på endpointen — en tränare för
/// Gul når aldrig Blås roster (§KM.3). Frågan filtrerar dessutom svaret till just det laget, så
/// inga andra lags barn kan slinka med även om laget delar trupp.
/// </para>
/// </summary>
public sealed record GetTeamRosterQuery(string Slug) : IQuery<TeamRosterDto?>;

// Slugen kommer från URL:en. Utan validator hoppas kontrollen tyst över (samma mönster som #551);
// samma grammatik + längdtak som GetTeamEventsQueryValidator (#586).
internal sealed class GetTeamRosterQueryValidator : AbstractValidator<GetTeamRosterQuery>
{
    public GetTeamRosterQueryValidator()
    {
        RuleFor(query => query.Slug)
            .NotEmpty().WithMessage("Laget måste anges.")
            .MaximumLength(80).WithMessage("Lagnamnet är för långt.")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Laget kan bara innehålla små bokstäver, siffror och bindestreck.");
    }
}

internal sealed class GetTeamRosterQueryHandler(IChildRepository children)
    : IQueryHandler<GetTeamRosterQuery, TeamRosterDto?>
{
    public async Task<TeamRosterDto?> HandleAsync(
        GetTeamRosterQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var team = await children.FindTeamBySlugAsync(query.Slug, cancellationToken)
            .ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        // Truppens barn, sedan filtrerade till just det här laget. Otilldelade barn och andra
        // färg-lags barn hör inte till en lagtränares vy.
        var childList = (await children
                .GetChildrenForTruppAsync(team.AgeGroupId, cancellationToken).ConfigureAwait(false))
            .Where(child => child.TeamId == team.Id)
            .ToArray();

        var guardianships = await children
            .GetGuardianshipsForTruppAsync(team.AgeGroupId, cancellationToken).ConfigureAwait(false);

        var guardiansByChild = guardianships
            .GroupBy(g => g.ChildId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<GuardianRefDto>)[.. g.Select(x => x.ToRef())]);

        var childDtos = childList
            .Select(child => child.ToDto(
                guardiansByChild.TryGetValue(child.Id, out var guardians) ? guardians : []))
            .ToArray();

        return new TeamRosterDto(team.ToRosterTeam(), childDtos);
    }
}
