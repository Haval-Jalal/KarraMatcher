using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Cup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Cup;

/// <summary>
/// Adminens cup-lags-bygge (`#335`, slice 4 av #330): skapa, döpa om och ta bort cup-lag, och
/// placera de anmälda barnen i dem.
///
/// <para>
/// Cup-lag byggs på trupp-nivå (<c>AdminOfTrupp</c>), som cupens öppna anmälan. Truppens och
/// cupens id står i adressen; tjänsten prövar att cupen hör till truppen, att laget hör till
/// cupen, och att barnet är anmält (§KM.1/IDOR). Läsning av cup-lagen sker via cupens
/// sammanställning (<c>GET /api/v1/events/{id}/cup</c>), som alla truppens medlemmar når.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/events/{eventId:guid}/cup/teams")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class CupTeamAdminController(ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Skapar ett tomt cup-lag.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid truppId, Guid eventId, CupTeamNameRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await commands
            .SendAsync(new CreateCupTeamCommand(truppId, eventId, request.Name), cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome == CupTeamOutcome.Ok
            ? Created(
                $"/api/v1/admin/trupper/{truppId}/events/{eventId}/cup/teams/{result.TeamId}",
                new { id = result.TeamId })
            : ProblemFor(result.Outcome);
    }

    /// <summary>Byter namn på ett cup-lag.</summary>
    [HttpPut("{cupTeamId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rename(
        Guid truppId,
        Guid eventId,
        Guid cupTeamId,
        CupTeamNameRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outcome = await commands
            .SendAsync(
                new RenameCupTeamCommand(truppId, eventId, cupTeamId, request.Name), cancellationToken)
            .ConfigureAwait(false);

        return outcome == CupTeamOutcome.Ok ? NoContent() : ProblemFor(outcome);
    }

    /// <summary>Tar bort ett cup-lag (dess placeringar försvinner med det).</summary>
    [HttpDelete("{cupTeamId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        Guid truppId, Guid eventId, Guid cupTeamId, CancellationToken cancellationToken)
    {
        var outcome = await commands
            .SendAsync(new DeleteCupTeamCommand(truppId, eventId, cupTeamId), cancellationToken)
            .ConfigureAwait(false);

        return outcome == CupTeamOutcome.Ok ? NoContent() : ProblemFor(outcome);
    }

    /// <summary>Placerar ett anmält barn i cup-laget (flyttar om det redan står i ett annat).</summary>
    [HttpPut("{cupTeamId:guid}/children/{childId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assign(
        Guid truppId,
        Guid eventId,
        Guid cupTeamId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var outcome = await commands
            .SendAsync(
                new AssignCupChildCommand(truppId, eventId, cupTeamId, childId), cancellationToken)
            .ConfigureAwait(false);

        return outcome == CupTeamOutcome.Ok ? NoContent() : ProblemFor(outcome);
    }

    /// <summary>Tar bort ett barn ur cup-laget.</summary>
    [HttpDelete("{cupTeamId:guid}/children/{childId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Unassign(
        Guid truppId,
        Guid eventId,
        Guid cupTeamId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var outcome = await commands
            .SendAsync(
                new UnassignCupChildCommand(truppId, eventId, cupTeamId, childId), cancellationToken)
            .ConfigureAwait(false);

        return outcome == CupTeamOutcome.Ok ? NoContent() : ProblemFor(outcome);
    }

    private ObjectResult ProblemFor(CupTeamOutcome outcome) => outcome switch
    {
        CupTeamOutcome.NotACup => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Bara cuper har cup-lag",
            detail: "Cup-lag byggs för en cup, inte för en match eller träning."),

        CupTeamOutcome.ChildNotSignedUp => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Barnet är inte anmält",
            detail: "Bara barn som anmält sig till cupen kan placeras i ett cup-lag."),

        // EventNotInTrupp och TeamNotFound: samma 404 — avslöja inte andra truppers/cupers id.
        _ => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Finns inte",
            detail: "Kontrollera länken — cupen eller cup-laget kan ha tagits bort."),
    };
}

/// <summary>Namnet på ett cup-lag som admin skriver in (§KM.1: aldrig barn-PII).</summary>
public sealed record CupTeamNameRequest(string Name);
