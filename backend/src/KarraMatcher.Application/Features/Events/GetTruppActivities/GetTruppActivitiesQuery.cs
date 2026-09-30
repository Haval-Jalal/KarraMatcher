using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Events.GetTruppActivities;

/// <summary>
/// Alla aktiviteter i en trupp (`#334`, epic #330): matcher, träningar, cuper och övrigt, tvärs
/// över färg-lagen och inklusive trupp-vida händelser, i avsparksordning. Truppens medlemmar ser
/// hela listan (§KM.3) — admin med en skapa-ingång, förälder i läsläge.
/// </summary>
public sealed record GetTruppActivitiesQuery(Guid TruppId, Guid ActorAccountId)
    : IQuery<IReadOnlyList<TruppActivityDto>>;

/// <summary>
/// En aktivitet i trupp-listan: händelsen som appen visar den, plus dess lag (eller <c>null</c>
/// för en trupp-vid händelse — då finns ingen enskild lagfärg, `#332`). Samma form som
/// <see cref="EventDetailDto"/>, men utan truppens id — listan är redan scopad till en trupp.
/// </summary>
public sealed record TruppActivityDto(EventDto Event, TeamDto? Team);
