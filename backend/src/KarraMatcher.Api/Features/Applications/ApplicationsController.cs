using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Application.Features.Applications;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Applications;

/// <summary>
/// Förälderns sida av en ansökan (`#194`, §KM.3).
///
/// <para>
/// Både trupp-infon och ansökan kräver inloggning — en självansökande förälder har ändå ett
/// konto (till skillnad från en inbjuden, §KM.3). Truppens id står i adressen (från länken
/// klubben delat).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/trupper/{truppId:guid}")]
[Produces("application/json")]
public sealed class ApplicationsController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>
    /// Vilken trupp länken leder till.
    ///
    /// <para>
    /// Kräver inloggning (§KM.3): till skillnad från en inbjudan, där den inbjudne kan sakna
    /// konto, har en självansökande förälder ändå ett konto för att kunna ansöka — så
    /// truppnamnet visas efter inloggning, och ingen anonym yta öppnas i den stängda appen.
    /// </para>
    /// </summary>
    [HttpGet("apply-info")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplyInfoDto>> ApplyInfo(
        Guid truppId, CancellationToken cancellationToken)
    {
        var info = await queries
            .SendAsync(new GetApplyInfoQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return info is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Truppen finns inte",
                detail: "Kontrollera länken.")
            : Ok(info);
    }

    /// <summary>Ansöker om att gå med i truppen. Kräver inloggning.</summary>
    [HttpPost("applications")]
    [Authorize]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Apply(Guid truppId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new SubmitApplicationCommand(truppId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            AdminOutcome.Success => StatusCode(StatusCodes.Status201Created),
            AdminOutcome.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Du har redan ansökt",
                detail: "Du har redan en väntande ansökan eller är redan medlem i truppen."),
            _ => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Truppen finns inte",
                detail: "Kontrollera länken."),
        };
    }
}
