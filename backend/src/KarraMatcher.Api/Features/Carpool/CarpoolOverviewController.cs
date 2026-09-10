using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Carpool;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Carpool;

/// <summary>
/// Tränarens överblick över lagets samåkning (`#55`, §KM.12).
///
/// <para>
/// <b>Laget står i adressen.</b> Policyn <c>CoachOfTeam</c> prövas mot just den slugen, så
/// en tränare för Gul kan inte se Blås samåkning — det finns inget lagfält att skicka i
/// stället.
/// </para>
///
/// <para>
/// Den enda läsningen i appen som ligger under <c>api/v1/teams</c> och ändå kräver
/// inloggning. Undantaget är infört med avsikt i <c>GuestAccessTests</c>: schemat är
/// allas, men vem som kör vem är lagets egen sak.
/// </para>
///
/// <para>
/// Aldrig edge-cachad. Svaret innehåller förarnas notiser, och en delad cache hade kunnat
/// leverera dem till nästa läsare (§KM.11).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}/carpool")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
public sealed class CarpoolOverviewController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Lagets kommande matcher med sin samåkning.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Overview(string slug, CancellationToken cancellationToken)
    {
        var overview = await queries
            .SendAsync(new TeamCarpoolOverviewQuery(slug, ActorId()), cancellationToken)
            .ConfigureAwait(false);

        return overview is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Laget finns inte",
                detail: "Kontrollera adressen.")
            : Ok(overview);
    }

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}
