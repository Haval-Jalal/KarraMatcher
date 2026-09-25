using KarraMatcher.Application.Features.Events;

namespace KarraMatcher.Application.Features.Teams;

/// <summary>
/// Ett lag och dess händelser — svaret appen bygger hela schemavyn av.
/// <paramref name="TruppId"/> är åldersgruppens id: klienten avgör med det om den inloggade är
/// tränare för lagets trupp och därför får sköta schemat (`#287`).
/// </summary>
public sealed record TeamEventsDto(TeamDto Team, IReadOnlyList<EventDto> Events, Guid TruppId);
