using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Matches;
using KarraMatcher.Application.Features.Matches.GetMatch;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Matches;

/// <summary>
/// Enskilda matcher.
///
/// <para>
/// <b>Stängd i v2 (§KM.3, `#191`):</b> bara medlemmar av matchens lag ser den. Superadmin
/// ser allt. Ingen publik läsning, ingen edge-cache.
/// </para>
///
/// <para>
/// Matchdetaljsidan behöver mer än listan visar: adressen till kartlänken och
/// koordinaterna till väderprognosen. Koordinaterna kommer alltid från vår egen
/// <c>Venue</c>-tabell och aldrig från användarindata — det är vad SSRF-regeln i
/// CLAUDE.md kräver av det utgående väderanropet.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/matches")]
[Produces("application/json")]
[Authorize]
public sealed class MatchesController(IQueryDispatcher dispatcher) : ControllerBase
{
    /// <summary>En match med spelplats, koordinater och lag. Kräver medlemskap i matchens lag.</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.MemberOfMatch)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MatchDetailDto>> GetMatch(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher
            .SendAsync(new GetMatchQuery(id), cancellationToken)
            .ConfigureAwait(false);

        // En okänd match är en gammal länk, inte ett systemfel. Kan mycket väl hända:
        // en förälder öppnar en kalenderpost från förra säsongen.
        return result is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Matchen finns inte",
                detail: "Kontrollera länken — matchen kan ha tagits bort.")
            : Ok(result);
    }
}
