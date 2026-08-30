using KarraMatcher.Api.Caching;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Matches;
using KarraMatcher.Application.Features.Matches.GetMatch;

using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Matches;

/// <summary>
/// Enskilda matcher. Publik och oautentiserad (§KM.3).
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
public sealed class MatchesController(IQueryDispatcher dispatcher) : ControllerBase
{
    /// <summary>En match med spelplats, koordinater och lag.</summary>
    [HttpGet("{id:guid}")]
    [EdgeCache(EdgeCacheProfile.MatchDetail)]
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
