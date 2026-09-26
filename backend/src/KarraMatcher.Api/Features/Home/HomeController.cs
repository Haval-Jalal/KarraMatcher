using System.Security.Claims;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Home;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Home;

/// <summary>
/// Hem-vyns sammanställning för den inloggade ("allt samlat"): nästa händelse, obesvarade
/// kallelser och det senaste i chatten.
///
/// <para>
/// Kräver bara inloggning — ingen policy per objekt. Sammanställningen scopas server-side till
/// kontots egna medlemskap (queryn får kontot ur token, aldrig ur kroppen), så en medlem kan
/// bara hämta sin egen översikt (§KM.3).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/hem")]
[Produces("application/json")]
[Authorize]
public sealed class HomeController(IQueryDispatcher dispatcher) : ControllerBase
{
    /// <summary>Hämtar den inloggades hem-sammanställning.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<HomeSummaryDto>> Get(CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var summary = await dispatcher
            .SendAsync(new GetHomeSummaryQuery(actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(summary);
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
