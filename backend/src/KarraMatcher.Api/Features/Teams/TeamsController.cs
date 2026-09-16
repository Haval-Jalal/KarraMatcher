using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;
using KarraMatcher.Application.Features.Teams.GetTeamMatches;
using KarraMatcher.Application.Features.Teams.GetTeams;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Teams;

/// <summary>
/// Lagen och deras matcher.
///
/// <para>
/// <b>Stängd i v2 (§KM.3, `#191`):</b> inloggning räcker inte — man ser bara de lag man är
/// medlem av (admin, tränare eller vårdnadshavare). Superadmin ser alla. Ingen publik
/// läsning, ingen edge-cache.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams")]
[Produces("application/json")]
[Authorize]
public sealed class TeamsController(
    IQueryDispatcher dispatcher,
    IMembershipService membership) : ControllerBase
{
    /// <summary>Lagen den inloggade är medlem av — för lagväljaren.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetTeams(
        CancellationToken cancellationToken)
    {
        var accountId = Membership.AccountId(User);

        if (accountId is null)
        {
            return Unauthorized();
        }

        var teams = await dispatcher
            .SendAsync(new GetTeamsQuery(), cancellationToken)
            .ConfigureAwait(false);

        var memberSlugs = await membership
            .MemberTeamSlugsAsync(accountId.Value, cancellationToken)
            .ConfigureAwait(false);

        var visible = new HashSet<string>(memberSlugs, StringComparer.Ordinal);

        return Ok(teams.Where(t => visible.Contains(t.Slug)).ToArray());
    }

    /// <summary>Ett lags matcher, sorterade på avspark. Kräver medlemskap i laget.</summary>
    [HttpGet("{slug}/matches")]
    [Authorize(Policy = AuthorizationPolicies.MemberOfTeam)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMatchesDto>> GetTeamMatches(
        string slug,
        CancellationToken cancellationToken)
    {
        var result = await dispatcher
            .SendAsync(new GetTeamMatchesQuery(slug), cancellationToken)
            .ConfigureAwait(false);

        // Ett okänt lag är inte ett fel i systemet utan en felaktig länk. 404 med
        // ProblemDetails, aldrig ett tomt schema som ser ut som en avslutad säsong.
        return result is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Laget finns inte",
                detail: "Kontrollera länken — laget kan ha bytt namn.")
            : Ok(result);
    }
}
