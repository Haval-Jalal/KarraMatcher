using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Attendance;
using KarraMatcher.Domain.Attendance;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Vårdnadshavarens svar på en kallelse, per barn (§KM.7, `#199`).
///
/// <h3>Inte MemberOfEvent — objektnivå i stället</h3>
///
/// <para>
/// En kallelse kan nå barn ur flera lag (ett lag fylls på vid behov), så en vårdnadshavare
/// vars barn kallats in från ett annat lag är <em>inte</em> medlem av händelsens lag. Därför
/// räcker inloggning här, och behörigheten avgörs per barn: man ser bara sina egna kallade
/// barn, och kan bara svara för ett barn man är vårdnadshavare för och som faktiskt kallats.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/events/{eventId:guid}/kallelse")]
[Produces("application/json")]
[Authorize]
public sealed class KallelseController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Mina egna kallade barn för händelsen, med deras svar. 404 när kallelsen är avslagen.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Mine(Guid eventId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var mine = await queries
            .SendAsync(new GetMyKallelseQuery(eventId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return mine is null ? NotFoundForEvent() : Ok(mine);
    }

    /// <summary>Svarar Ja/Nej för ett av mina barn. Går att ändra ända fram till avspark.</summary>
    [HttpPut("children/{childId:guid}")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Respond(
        Guid eventId,
        Guid childId,
        RespondRequest request,
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
                new RespondToKallelseCommand(eventId, childId, actor.Value, request.Reply),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            RespondOutcome.Saved => NoContent(),

            RespondOutcome.Closed => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Händelsen har börjat",
                detail: "Det går inte att svara på en händelse som redan ägt rum."),

            // NotGuardian, NotInvited och NotCalled ger alla samma 404 — annars gick det att
            // lista ut vilka barn som finns eller är kallade genom att prova (§KM.1).
            _ => NotFoundForEvent(),
        };
    }

    private ObjectResult NotFoundForEvent() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Ingen kallelse här",
        detail: "Kontrollera länken — händelsen eller kallelsen kan ha tagits bort.");

    private ObjectResult Unauthenticated() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sessionen gäller inte längre",
        detail: "Logga in igen.");

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>Vårdnadshavarens svar för ett barn: Ja (Coming) eller Nej (NotComing).</summary>
public sealed record RespondRequest(AttendanceReply Reply);
