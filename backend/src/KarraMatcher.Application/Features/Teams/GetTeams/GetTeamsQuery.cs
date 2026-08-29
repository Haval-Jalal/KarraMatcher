using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Teams.GetTeams;

/// <summary>Alla lag, för lagväljaren. Inga parametrar och inget att validera.</summary>
public sealed record GetTeamsQuery : IQuery<IReadOnlyList<TeamDto>>;
