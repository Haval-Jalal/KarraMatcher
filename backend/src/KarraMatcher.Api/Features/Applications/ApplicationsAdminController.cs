using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Application.Features.Applications;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Applications;

/// <summary>
/// Adminens ansökningskö för en trupp (`#194`, §KM.3). Bara admin för truppen (eller
/// superadmin) når hit — truppens id står i adressen.
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/applications")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class ApplicationsAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Väntande ansökningar för truppen.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ApplicationDto>>> List(
        Guid truppId, CancellationToken cancellationToken)
    {
        var pending = await queries
            .SendAsync(new GetTruppApplicationsQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(pending);
    }

    /// <summary>Godkänner en ansökan — skapar förälderns medlemskap.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new ApproveApplicationCommand(truppId, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return MapResolve(outcome);
    }

    /// <summary>Nekar en ansökan.</summary>
    [HttpPost("{id:guid}/deny")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deny(Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new DenyApplicationCommand(truppId, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return MapResolve(outcome);
    }

    private IActionResult MapResolve(AdminOutcome outcome) => outcome switch
    {
        AdminOutcome.Success => NoContent(),
        AdminOutcome.Conflict => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Ansökan är redan avgjord",
            detail: "Den här ansökan har redan godkänts eller nekats."),
        _ => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Ansökan finns inte",
            detail: "Kontrollera länken — ansökan kan ha tagits bort."),
    };
}
