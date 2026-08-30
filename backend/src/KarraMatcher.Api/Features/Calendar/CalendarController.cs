using KarraMatcher.Api.Caching;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Calendar.GetTeamCalendar;

using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Calendar;

/// <summary>
/// Lagets kalenderfeed. Publik och oautentiserad (§KM.3 och §KM.4).
///
/// <para>
/// Sannolikt appens mest värdefulla funktion: föräldern prenumererar en gång och slipper
/// sedan öppna appen. Nya matcher dyker upp av sig själva och en flyttad match flyttar sig
/// i telefonens egen kalender. Det är också vad som håller appen relevant mellan
/// säsongerna, och fallbacken för iOS-föräldrar som inte installerar den på hemskärmen.
/// </para>
///
/// <para>
/// Adressen ligger avsiktligt utanför <c>/api</c>: den klistras in i en kalenderapp av en
/// människa, och <c>/calendar/gul.ics</c> är kortare att läsa upp för någon än
/// <c>/api/v1/calendar/gul.ics</c>. Vercel-rewriten är utökad för att täcka den.
/// </para>
/// </summary>
[ApiController]
[Route("calendar")]
public sealed class CalendarController(IQueryDispatcher dispatcher) : ControllerBase
{
    /// <summary>Lagets alla matcher som en kalenderprenumeration.</summary>
    [HttpGet("{slug}.ics")]
    [EdgeCache(EdgeCacheProfile.Calendar)]
    [Produces("text/calendar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTeamCalendar(string slug, CancellationToken cancellationToken)
    {
        var ics = await dispatcher
            .SendAsync(new GetTeamCalendarQuery(slug), cancellationToken)
            .ConfigureAwait(false);

        if (ics is null)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Laget finns inte",
                detail: "Kontrollera länken — laget kan ha bytt namn.");
        }

        // Teckenkodningen måste anges. Utan den gissar en del kalenderappar Latin-1 och
        // visar "BlÃ¥" i stället för "Blå" i varje rubrik.
        return Content(ics, "text/calendar; charset=utf-8");
    }
}
