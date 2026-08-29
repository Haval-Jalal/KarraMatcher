using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Teams.GetTeamMatches;

/// <summary>Ett lags hela matchschema. Returnerar null om laget inte finns.</summary>
public sealed record GetTeamMatchesQuery(string Slug) : IQuery<TeamMatchesDto?>;
