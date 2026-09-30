using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Teams.GetTeamEvents;

/// <summary>
/// Ett lags schema av händelser för den inloggade. Returnerar null om laget inte finns.
/// <paramref name="ActorAccountId"/> avgör match-synligheten: matcher man inte är kallad på
/// gallras bort om man inte sköter laget/truppen (§KM.7, ägarbeslut).
/// </summary>
public sealed record GetTeamEventsQuery(string Slug, Guid ActorAccountId) : IQuery<TeamEventsDto?>;
