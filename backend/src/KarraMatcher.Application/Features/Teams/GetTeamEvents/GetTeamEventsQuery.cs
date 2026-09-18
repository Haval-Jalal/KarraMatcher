using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Teams.GetTeamEvents;

/// <summary>Ett lags hela schema av händelser. Returnerar null om laget inte finns.</summary>
public sealed record GetTeamEventsQuery(string Slug) : IQuery<TeamEventsDto?>;
