using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Calendar;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Calendar;

/// <summary>
/// Kalender bakom medlemskap (§KM.4, återinförd som privat feed).
///
/// <para>
/// Feeden nås anonymt — nyckeln i URL:en <em>är</em> behörigheten, för en kalender-app kan inte
/// logga in. Den är ogissbar, pekar på exakt ett konto, och feeden bär bara händelser, aldrig
/// barn-PII (§KM.1). Att skapa och återkalla nyckeln kräver inloggning: den hör till ett konto.
/// Svaret får <c>private, no-store</c> som allt annat (§KM.11) — en personlig feed edge-cachas
/// aldrig.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/kalender")]
public sealed class CalendarController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    /// <summary>Kalender-feeden för en nyckel. Anonym; okänd nyckel ger 404.</summary>
    [HttpGet("{token}.ics")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Feed(string token, CancellationToken cancellationToken)
    {
        var ics = await queries
            .SendAsync(new GetCalendarFeedQuery(token), cancellationToken)
            .ConfigureAwait(false);

        if (ics is null)
        {
            return NotFound();
        }

        return Content(ics, "text/calendar; charset=utf-8");
    }

    /// <summary>Den inloggades kalender-länk (skapar en nyckel vid första anropet).</summary>
    [HttpGet("min")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CalendarLinkDto>> Mine(CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var link = await queries
            .SendAsync(new GetCalendarLinkQuery(actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(link);
    }

    /// <summary>Byter ut kalender-nyckeln — den gamla länken slutar fungera direkt.</summary>
    [HttpPost("aterkalla")]
    [Authorize]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CalendarLinkDto>> Regenerate(CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var link = await commands
            .SendAsync(new RegenerateCalendarTokenCommand(actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(link);
    }

    private ObjectResult Unauthenticated() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sessionen gäller inte längre",
        detail: "Logga in igen.");

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
