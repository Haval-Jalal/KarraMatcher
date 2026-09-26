using System.Security.Claims;

using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Cup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Cup;

/// <summary>
/// Tränarens sida av cupens öppna anmälan (`#295`): sätta platstaket.
///
/// <para>
/// En cup drar barn tvärs över truppens färg-lag, så anmälan sköts på trupp-nivå
/// (<c>AdminOfTrupp</c>) — inte per lag. Tränaren väljer bara <em>hur många</em> platser; vilka
/// barn som kommer avgörs av först-till-kvarn, inte av en inbjudningslista (till skillnad från
/// den riktade kallelsen, `#199`).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/events/{eventId:guid}/cup")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class CupAdminController(ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Öppnar (eller ändrar) cupens platstak.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Open(
        Guid truppId,
        Guid eventId,
        OpenCupRequest request,
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
                new OpenCupCommand(truppId, eventId, request.Capacity, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            OpenCupOutcome.Opened => NoContent(),

            OpenCupOutcome.NotACup => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Bara cuper har öppen anmälan",
                detail: "Öppen anmälan med platstak gäller cuper. En match eller träning använder kallelsen i stället (§KM.7)."),

            // EventNotInTrupp: samma 404 som en okänd händelse — avslöja inte andra truppers id.
            _ => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Händelsen finns inte",
                detail: "Kontrollera länken — händelsen kan ha tagits bort."),
        };
    }
}

/// <summary>
/// Vårdnadshavarens sida av cupens öppna anmälan (`#295`): anmäla ett barn, dra tillbaka, och se
/// läget. Först till kvarn — en full cup avvisas server-side (§KM.12-stil), aldrig bara en dold
/// knapp.
///
/// <para>
/// Ingen lag-policy: en cup är trupp-vid, så en förälder vars barn ligger i ett annat färg-lag
/// ska ändå kunna anmäla. Åtkomsten avgörs på objektet i stället — man når bara <em>sitt eget</em>
/// barn, och bara barn i cupens trupp. Sammanställningen namnger barn (§KM.1) och kräver
/// trupp-medlemskap, vilket prövas i tjänsten.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/events/{eventId:guid}/cup")]
[Produces("application/json")]
[Authorize]
public sealed class CupController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Cupens anmälningsläge: platstak, tagna platser och de anmälda barnen.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CupSummaryDto>> Summary(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var summary = await queries
            .SendAsync(new GetCupSummaryQuery(eventId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return summary is null ? NotFoundEvent() : Ok(summary);
    }

    /// <summary>Anmäler ett eget barn. Först till kvarn — full cup ger 409.</summary>
    [HttpPost("children/{childId:guid}")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SignUp(
        Guid eventId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new SignUpForCupCommand(eventId, childId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            CupSignupOutcome.SignedUp => NoContent(),

            CupSignupOutcome.Full => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Cupen är fullbokad",
                detail: "Alla platser är tagna. Hör med tränaren om det öppnas fler."),

            CupSignupOutcome.SignupNotOpen => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Anmälan är inte öppen än",
                detail: "Tränaren har inte öppnat anmälan för den här cupen."),

            CupSignupOutcome.AlreadySignedUp => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Barnet är redan anmält",
                detail: "Platsen är redan tagen för det här barnet."),

            // NotACup, NotGuardian, ChildNotInTrupp: samma 404 — avslöja aldrig ett barns
            // existens eller en annans barn för den som provar sig fram (§KM.1/IDOR).
            _ => NotFoundEvent(),
        };
    }

    /// <summary>Drar tillbaka en anmälan och frigör platsen.</summary>
    [HttpDelete("children/{childId:guid}")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(
        Guid eventId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new WithdrawCupSignupCommand(eventId, childId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        // Withdrawn → 204; NotSignedUp och NotGuardian → samma 404 (avslöja inget).
        return outcome == CupWithdrawOutcome.Withdrawn ? NoContent() : NotFoundEvent();
    }

    private ObjectResult NotFoundEvent() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Cupen finns inte",
        detail: "Kontrollera länken — cupen kan ha tagits bort, eller så är det inte ditt barn.");

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

/// <summary>
/// Truppens cuper med anmälningsläge (`#304`): en trupp-vid lista, eftersom en cup drar barn
/// tvärs över färg-lagen. Bara truppens medlemmar (<c>MemberOfTrupp</c>).
/// </summary>
[ApiController]
[Route("api/v1/trupper/{truppId:guid}/cups")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.MemberOfTrupp)]
public sealed class TruppCupsController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Truppens cuper i avsparksordning, med platser kvar / fullt.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<CupListItemDto>>> List(
        Guid truppId, CancellationToken cancellationToken)
    {
        var cups = await queries
            .SendAsync(new GetTruppCupsQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(cups);
    }
}

/// <summary>Det tränaren skickar för att öppna anmälan: antal platser.</summary>
public sealed record OpenCupRequest(int Capacity);
