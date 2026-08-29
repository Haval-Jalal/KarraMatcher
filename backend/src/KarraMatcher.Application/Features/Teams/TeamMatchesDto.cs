using KarraMatcher.Application.Features.Matches;

namespace KarraMatcher.Application.Features.Teams;

/// <summary>Ett lag och dess matcher — svaret appen bygger hela schemavyn av.</summary>
public sealed record TeamMatchesDto(TeamDto Team, IReadOnlyList<MatchDto> Matches);
