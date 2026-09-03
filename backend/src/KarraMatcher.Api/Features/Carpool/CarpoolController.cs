using System.Security.Claims;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Carpool;

using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Carpool;

/// <summary>
/// Samåkningen som vem som helst får se (§KM.3).
///
/// <h3>Skild från skrivningen, inte av prydlighet</h3>
///
/// <para>
/// Samma uppdelning som <c>MatchesController</c> och <c>MatchAdminController</c>: den ena
/// läser och är öppen, den andra skriver och kräver konto. Att blanda dem i en klass med
/// <c>[Authorize]</c> plus <c>[AllowAnonymous]</c> hade fungerat vid körning — men då bär
/// den publika läsningen auktoriseringsmetadata, och gästvakten kan inte längre skilja en
/// öppen endpoint från en som råkat bli stängd.
/// </para>
///
/// <h3>Svaret får inte hamna i en delad cache</h3>
///
/// <para>
/// Listan innehåller olika mycket beroende på vem som frågar: en inloggad ser förarens
/// notis, en gäst gör det inte. Endpointen är därför medvetet <b>inte</b> märkt med
/// <c>WithEdgeCache</c> och får då <c>private</c> som allt annat. Kallstarten på Render får
/// kosta här — alternativet är att en förälders fritext levereras till någon annan från
/// Vercels edge.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/matches/{matchId:guid}/carpool")]
[Produces("application/json")]
public sealed class CarpoolController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Matchens öppna erbjudanden.</summary>
    /// <remarks>
    /// Tillbakadragna kommer inte med — de syns inte längre som bokningsbara.
    /// </remarks>
    [HttpGet("offers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CarpoolOfferDto>>> List(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        /*
         * Lasaren far identifiera sig frivilligt. Ar token giltig kommer notisen med, annars
         * inte -- en gast far alltsa erbjudandet utan fritexten, inte ett avslag.
         */
        var offers = await queries
            .SendAsync(new ListCarpoolOffersQuery(matchId, ActorId()), cancellationToken)
            .ConfigureAwait(false);

        return Ok(offers);
    }

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
