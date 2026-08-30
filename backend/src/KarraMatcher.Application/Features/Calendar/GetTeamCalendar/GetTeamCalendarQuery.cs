using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Calendar.GetTeamCalendar;

/// <summary>Lagets kalenderfeed som ICS-text. Null om laget inte finns.</summary>
public sealed record GetTeamCalendarQuery(string Slug) : IQuery<string?>;
