using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Events;
using KarraMatcher.Application.Features.Events.GetEvent;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Events;

/// <summary>
/// Enskilda händelser — matcher, träningar och övrigt (`#198`).
///
/// <para>
/// <b>Stängd i v2 (§KM.3, `#191`):</b> bara medlemmar av händelsens lag ser den. Superadmin
/// ser allt. Ingen publik läsning, ingen edge-cache.
/// </para>
///
/// <para>
/// Detaljsidan behöver mer än listan visar: adressen till kartlänken och koordinaterna till
/// väderprognosen. Koordinaterna kommer alltid från vår egen <c>Venue</c>-tabell och aldrig
/// från användarindata — det är vad SSRF-regeln i CLAUDE.md kräver av väderanropet.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/events")]
[Produces("application/json")]
[Authorize]
public sealed class EventsController(IQueryDispatcher dispatcher) : ControllerBase
{
    /// <summary>En händelse med spelplats, koordinater och lag. Kräver medlemskap i lagets.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.MemberOfEvent)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> GetEvent(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher
            .SendAsync(new GetEventQuery(id), cancellationToken)
            .ConfigureAwait(false);

        // En okänd händelse är en gammal länk, inte ett systemfel. Kan mycket väl hända:
        // en förälder öppnar en kalenderpost från förra säsongen.
        return result is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Händelsen finns inte",
                detail: "Kontrollera länken — händelsen kan ha tagits bort.")
            : Ok(result);
    }
}
