using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Adminens tillsättning av tränare per lag (§KM.3, `#197`). Bara admin för truppen (eller
/// superadmin) når hit — truppens id står i adressen, och laget kontrolleras höra dit.
///
/// <para>
/// En tränare tilldelas ett befintligt konto via dess adress; en person utan konto bjuds in i
/// `#193`. Rollen blir ett <c>coach</c>-anspråk på lagets slug och låser upp lagets endpoints
/// via <see cref="AuthorizationPolicies.CoachOfTeam"/>.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class TeamCoachesController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Truppens lag med sina tränare.</summary>
    [HttpGet("coaches")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TruppCoachesDto>> List(
        Guid truppId, CancellationToken cancellationToken)
    {
        var coaches = await queries.SendAsync(new GetTruppCoachesQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(coaches);
    }

    /// <summary>Tillsätter en tränare för ett lag via kontots adress.</summary>
    [HttpPost("teams/{teamId:guid}/coaches")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Grant(
        Guid truppId, Guid teamId, GrantCoachRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new GrantCoachCommand(truppId, teamId, request.Email, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(
            result,
            dto => Created(
                $"/api/v1/admin/trupper/{truppId}/teams/{teamId}/coaches/{dto.AccountId}", dto));
    }

    /// <summary>Avsätter en tränare från ett lag.</summary>
    [HttpDelete("teams/{teamId:guid}/coaches/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid truppId, Guid teamId, Guid accountId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new RevokeCoachCommand(truppId, teamId, accountId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome, NoContent());
    }
}

/// <summary>Adressen till kontot som ska bli tränare.</summary>
public sealed record GrantCoachRequest(string Email);
