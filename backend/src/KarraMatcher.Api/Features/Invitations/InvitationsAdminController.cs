using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Invitations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Invitations;

/// <summary>
/// Adminens inbjudningar för en trupp (`#193`, §KM.3). Bara admin för truppen (eller
/// superadmin) når hit — truppens id står i adressen.
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/invitations")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class InvitationsAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Väntande inbjudningar för truppen.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<InvitationDto>>> List(
        Guid truppId, CancellationToken cancellationToken)
    {
        var pending = await queries
            .SendAsync(new GetTruppInvitationsQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(pending);
    }

    /// <summary>Skapar en inbjudan och mejlar länken.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        Guid truppId, InvitationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new CreateInvitationCommand(truppId, request.Email, request.TeamId, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(
            result, dto => Created($"/api/v1/admin/trupper/{truppId}/invitations", dto));
    }

    /// <summary>Återkallar en väntande inbjudan.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(
        Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new RevokeInvitationCommand(truppId, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome, NoContent());
    }
}

/// <summary>Det admin fyller i för en inbjudan: adress och valfritt lag-förslag.</summary>
public sealed record InvitationRequest(string Email, Guid? TeamId);
