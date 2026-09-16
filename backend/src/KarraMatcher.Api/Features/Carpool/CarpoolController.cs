using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Carpool;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Carpool;

/// <summary>
/// Samåkningens erbjudanden för en match.
///
/// <para>
/// <b>Stängd i v2 (§KM.3, `#191`):</b> bara medlemmar av matchens lag ser erbjudandena —
/// det finns inte längre någon publik gäst. Föräldrarnas fritext (förarens notis) syns
/// därmed aldrig utanför laget.
/// </para>
///
/// <h3>Svaret får inte hamna i en delad cache</h3>
///
/// <para>
/// Listan bär föräldrafritext och är per medlem. Som alla svar i den stängda appen får den
/// <c>private, no-store</c> — den kan aldrig hamna på Vercels delade edge och levereras till
/// någon annan.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/matches/{matchId:guid}/carpool")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.MemberOfMatch)]
public sealed class CarpoolController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Matchens öppna erbjudanden.</summary>
    /// <remarks>
    /// Tillbakadragna kommer inte med — de syns inte längre som bokningsbara.
    /// </remarks>
    [HttpGet("offers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CarpoolOfferDto>>> List(
        Guid matchId,
        CancellationToken cancellationToken)
    {
        /*
         * Lasaren ar alltid en inloggad medlem av matchens lag (v2). ActorId har darfor
         * ett varde och notisen foljer med -- ingen gast langre.
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
