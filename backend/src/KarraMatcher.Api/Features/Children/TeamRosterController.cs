using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Children;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Children;

/// <summary>
/// Lagtränarens läsvy över sitt lag (`#redesign`): barnen och deras vårdnadshavare.
///
/// <para>
/// <b>Laget står i adressen.</b> Policyn <c>CoachOfTeam</c> prövas mot just den slugen, så en
/// tränare för Gul inte kan läsa Blås barn — det finns inget lagfält att byta ut (§KM.3). Admin
/// och superadmin släpps också igenom av policyn. Endast läsning: att ändra barn och koppla
/// vårdnadshavare är fortsatt adminens sak (<see cref="ChildrenAdminController"/>).
/// </para>
///
/// <para>
/// Aldrig edge-cachad. Svaret bär vårdnadshavarnas mejladresser (vuxen-PII), och en delad cache
/// hade kunnat leverera dem till nästa läsare (§KM.11). All <c>/api/</c>-trafik är
/// <c>private, no-store</c>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}/roster")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
public sealed class TeamRosterController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Lagets barn med kopplade vårdnadshavare.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Roster(string slug, CancellationToken cancellationToken)
    {
        var roster = await queries.SendAsync(new GetTeamRosterQuery(slug), cancellationToken)
            .ConfigureAwait(false);

        return roster is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Laget finns inte",
                detail: "Kontrollera adressen.")
            : Ok(roster);
    }
}
