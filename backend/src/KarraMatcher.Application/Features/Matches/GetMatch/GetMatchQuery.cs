using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Matches.GetMatch;

/// <summary>En enskild match. Returnerar null om den inte finns.</summary>
public sealed record GetMatchQuery(Guid Id) : IQuery<MatchDetailDto?>;
