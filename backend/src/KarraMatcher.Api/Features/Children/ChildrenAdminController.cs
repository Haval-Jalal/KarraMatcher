using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Application.Features.Children;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Children;

/// <summary>
/// Adminens barnhantering för en trupp (§KM.1, `#196`). Bara admin för truppen (eller
/// superadmin) når hit — truppens id står i adressen, och varje barn kontrolleras höra dit.
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/children")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class ChildrenAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Truppens överblick: lag och barn (grupperas per lag i vyn).</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TruppRosterDto>> Roster(
        Guid truppId, CancellationToken cancellationToken)
    {
        var roster = await queries.SendAsync(new GetTruppRosterQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(roster);
    }

    /// <summary>Skapar ett barn i truppen, valfritt direkt i ett lag.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        Guid truppId, ChildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new CreateChildCommand(
                    truppId, request.FirstName, request.LastInitial, request.TeamId, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, dto => Created($"/api/v1/admin/trupper/{truppId}/children/{dto.Id}", dto));
    }

    /// <summary>Ändrar ett barns namn och lag (flytta mellan lag; otilldelad = utan lag).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid truppId, Guid id, ChildRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new UpdateChildCommand(
                    truppId, id, request.FirstName, request.LastInitial, request.TeamId, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, Ok);
    }

    /// <summary>Tar bort ett barn ur truppen.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new DeleteChildCommand(truppId, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome, NoContent());
    }

    /// <summary>Kopplar en vårdnadshavare till barnet (kräver samtycke, §KM.6).</summary>
    [HttpPost("{id:guid}/guardians")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> LinkGuardian(
        Guid truppId, Guid id, LinkGuardianRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new LinkGuardianCommand(truppId, id, request.Email, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            LinkGuardianOutcome.Linked => NoContent(),
            LinkGuardianOutcome.ChildNotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Barnet finns inte",
                detail: "Kontrollera länken — barnet kan ha tagits bort."),
            LinkGuardianOutcome.AccountNotFound => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Inget konto",
                detail: "Det finns inget konto med den adressen. Bjud in vårdnadshavaren först."),
            LinkGuardianOutcome.NotTruppMember => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Inte medlem i truppen",
                detail: "Vårdnadshavaren har inte gått med i truppen än."),
            LinkGuardianOutcome.NoConsent => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Samtycke saknas",
                detail: "Vårdnadshavaren måste godkänna samtyckestexten innan barnet kopplas (§KM.6)."),
            LinkGuardianOutcome.AlreadyLinked => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Redan kopplad",
                detail: "Kontot är redan vårdnadshavare för barnet."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Okänt fel"),
        };
    }

    /// <summary>Kopplar bort en vårdnadshavare från barnet.</summary>
    [HttpDelete("{id:guid}/guardians/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlinkGuardian(
        Guid truppId, Guid id, Guid accountId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new UnlinkGuardianCommand(truppId, id, accountId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome, NoContent());
    }
}

/// <summary>Det admin fyller i för ett barn (§KM.1: förnamn + initial, aldrig hela efternamnet).</summary>
public sealed record ChildRequest(string FirstName, string LastInitial, Guid? TeamId);

/// <summary>Adressen till kontot som ska kopplas som vårdnadshavare.</summary>
public sealed record LinkGuardianRequest(string Email);
