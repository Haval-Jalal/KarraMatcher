using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Superadmins tillsättning av admins per trupp (§KM.3, `#192`). Bara superadmin når hit.
///
/// <para>
/// Adressen bär truppens id. En admin tilldelas ett befintligt konto via dess adress; en
/// person utan konto bjuds in i `#193`.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/admins")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
[RequireCsrfToken]
public sealed class TruppAdminsController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Truppens admins.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TruppAdminDto>>> List(
        Guid truppId, CancellationToken cancellationToken)
    {
        var admins = await queries.SendAsync(new GetTruppAdminsQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(admins);
    }

    /// <summary>Tilldelar en admin till truppen via kontots adress.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Grant(
        Guid truppId, GrantAdminRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(new GrantAdminCommand(truppId, request.Email, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(
            result, dto => Created($"/api/v1/admin/trupper/{truppId}/admins/{dto.AccountId}", dto));
    }

    /// <summary>Återkallar en admins roll för truppen.</summary>
    [HttpDelete("{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Revoke(
        Guid truppId, Guid accountId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new RevokeAdminCommand(truppId, accountId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome, NoContent());
    }
}

/// <summary>Adressen till kontot som ska bli admin.</summary>
public sealed record GrantAdminRequest(string Email);
