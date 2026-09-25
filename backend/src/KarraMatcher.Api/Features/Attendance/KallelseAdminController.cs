using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Attendance;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Adminens riktade kallelse för en händelse (§KM.7, `#199`).
///
/// <h3>Trupp-admin, inte lag-tränare</h3>
///
/// <para>
/// Kallelsen kan plocka barn ur alla lag i truppen (ett lag fylls på vid behov), så den
/// sköts av en admin för truppen (<c>AdminOfTrupp</c>) — inte av en enskild lag-tränare. Alla
/// P16-tränare är admins. Truppens id står i adressen; händelsen kontrolleras höra dit, och
/// varje valt barn kontrolleras tillhöra truppen.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/events/{eventId:guid}/kallelse")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class KallelseAdminController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : AdminControllerBase
{
    /// <summary>Skickar/uppdaterar kallelsen: vilka barn ur truppen som kallas.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Set(
        Guid truppId,
        Guid eventId,
        SetKallelseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(
                new SetKallelseCommand(truppId, eventId, request.ChildIds ?? [], actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            SetKallelseOutcome.Set => NoContent(),

            SetKallelseOutcome.InvalidChild => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Ogiltigt barn",
                detail: "Ett valt barn hör inte till truppen."),

            SetKallelseOutcome.Disabled => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Kallelser är avslagna",
                detail: "Slå på kallelser för laget innan du skickar en kallelse (§KM.7)."),

            SetKallelseOutcome.EventNotInvitable => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Kallelse gäller inte den här händelsen",
                detail: "Bara matcher och träningar kan ha en kallelse — inte övriga händelser (§KM.7)."),

            // EventNotInTrupp: samma 404 som en okänd händelse — avslöja inte andra truppers id.
            _ => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Händelsen finns inte",
                detail: "Kontrollera länken — händelsen kan ha tagits bort."),
        };
    }

    /// <summary>Sammanställningen: kallade barn med lag och svar, samt Ja/Nej/ej-svarat-antal.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KallelseSummaryDto>> Summary(
        Guid truppId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var summary = await queries
            .SendAsync(new GetKallelseSummaryQuery(truppId, eventId), cancellationToken)
            .ConfigureAwait(false);

        return summary is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Händelsen finns inte",
                detail: "Kontrollera länken — händelsen kan ha tagits bort.")
            : Ok(summary);
    }

    /// <summary>Påminner vårdnadshavarna till de kallade barn som inte svarat.</summary>
    [HttpPost("remind")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remind(
        Guid truppId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var reminded = await commands
            .SendAsync(new RemindNonRespondersCommand(truppId, eventId), cancellationToken)
            .ConfigureAwait(false);

        return reminded is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Händelsen finns inte",
                detail: "Kontrollera länken — händelsen kan ha tagits bort.")
            : Ok(new AttendanceRemindResult(reminded.Value));
    }
}

/// <summary>Barnen som ska kallas — id ur truppen, tvärs över lagen vid behov.</summary>
public sealed record SetKallelseRequest(IReadOnlyList<Guid>? ChildIds);

/// <summary>Hur många barn som saknar svar och påmindes. Aldrig vilka — bara antalet.</summary>
public sealed record AttendanceRemindResult(int Reminded);
