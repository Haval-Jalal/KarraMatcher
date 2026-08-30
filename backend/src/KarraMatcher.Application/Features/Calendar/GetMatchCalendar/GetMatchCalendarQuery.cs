using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Calendar.GetMatchCalendar;

/// <summary>
/// En enskild match som kalenderfil, med ett förslag på filnamn.
/// Null om matchen inte finns.
/// </summary>
public sealed record GetMatchCalendarQuery(Guid Id) : IQuery<MatchCalendar?>;

/// <param name="Content">ICS-innehållet.</param>
/// <param name="FileName">
/// Filnamnet nedladdningen föreslår. Läsbart med flit — det är vad föräldern ser i
/// nedladdningslistan, och "kalender.ics" tre gånger går inte att skilja åt.
/// </param>
public sealed record MatchCalendar(string Content, string FileName);
