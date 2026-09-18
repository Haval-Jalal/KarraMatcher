using KarraMatcher.Application.Features.Events;

namespace KarraMatcher.Application.Features.Teams;

/// <summary>Ett lag och dess händelser — svaret appen bygger hela schemavyn av.</summary>
public sealed record TeamEventsDto(TeamDto Team, IReadOnlyList<EventDto> Events);
