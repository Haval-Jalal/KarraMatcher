using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Carpool;
using KarraMatcher.Domain.Carpool;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Carpool;

/// <summary>
/// Skjutsförfrågningar: en förälder ber om skjuts, och förare erbjuder plats (§KM.12, `#63`) —
/// spegelbilden av <see cref="CarpoolDriverController"/>.
///
/// <para>
/// Allt ligger under matchen och grindas av <c>MemberOfEvent</c>: bara medlemmar av matchens lag
/// ser och agerar på samåkningen, så föräldrarnas fritext aldrig når utanför laget (§KM.3). Vem
/// som får dra tillbaka eller svara på vad prövas i tjänsten, på raden självt.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/matches/{matchId:guid}/carpool")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.MemberOfEvent)]
[RequireCsrfToken]
public sealed class CarpoolRideController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Matchens öppna skjutsförfrågningar.</summary>
    [HttpGet("ride-requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(Guid matchId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var found = await queries
            .SendAsync(new ListCarpoolRideRequestsQuery(matchId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(found);
    }

    /// <summary>En förälder ber om skjuts.</summary>
    [HttpPost("ride-requests")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid matchId,
        CarpoolRideRequestRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var (outcome, created) = await commands
            .SendAsync(
                new CreateCarpoolRideRequestCommand(matchId, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            CarpoolRideRequestOutcome.Created => CreatedAtAction(
                actionName: nameof(List),
                routeValues: new { matchId },
                value: created),

            CarpoolRideRequestOutcome.NotAMatch => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Samåkning gäller bara matcher",
                detail: "En träning eller övrig händelse har ingen samåkning (§KM.12)."),

            _ => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Matchen finns inte",
                detail: "Kontrollera länken — matchen kan ha tagits bort."),
        };
    }

    /// <summary>
    /// Drar tillbaka en skjutsförfrågan.
    /// </summary>
    /// <remarks>
    /// Förfrågan raderas inte. Den blir kvar som tillbakadragen tills gallringen tar hela matchens
    /// samåkning 30 dagar efteråt (§KM.12) — en förare som erbjöd plats ska se vad som hände.
    /// </remarks>
    [HttpPost("ride-requests/{rideRequestId:guid}/withdraw")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(
        Guid rideRequestId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var withdrawn = await commands
            .SendAsync(
                new WithdrawCarpoolRideRequestCommand(rideRequestId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return withdrawn ? NoContent() : NotFoundForOwner();
    }

    /// <summary>
    /// En förfrågans platserbjudanden.
    /// </summary>
    /// <remarks>
    /// Den som frågade ser alla — det är hen som ska svara. En förare ser bara sitt eget. Hälsningen
    /// är fritext och får bara nå de inblandade (§KM.12), så filtreringen sitter i tjänsten.
    /// </remarks>
    [HttpGet("ride-requests/{rideRequestId:guid}/offers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Offers(
        Guid rideRequestId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var found = await queries
            .SendAsync(new ListCarpoolRideOffersQuery(rideRequestId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(found);
    }

    /// <summary>En förare erbjuder plats på en skjutsförfrågan.</summary>
    [HttpPost("ride-requests/{rideRequestId:guid}/offers")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Offer(
        Guid matchId,
        Guid rideRequestId,
        CarpoolRideOfferRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var (outcome, created) = await commands
            .SendAsync(
                new OfferCarpoolSeatCommand(matchId, rideRequestId, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            CarpoolRideOfferOutcome.Created => CreatedAtAction(
                actionName: nameof(Offers),
                routeValues: new { matchId, rideRequestId },
                value: created),

            CarpoolRideOfferOutcome.AlreadyOffered => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Du har redan erbjudit plats",
                detail: "Vänta på svar, eller återta ditt erbjudande först."),

            CarpoolRideOfferOutcome.OwnRequest => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Det är din egen förfrågan",
                detail: "Du kan inte erbjuda plats åt dig själv."),

            _ => NotFoundForRequest(),
        };
    }

    /// <summary>Den som frågade tackar ja till ett platserbjudande. Förfrågan blir löst.</summary>
    [HttpPost("ride-offers/{offerId:guid}/accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Accept(
        Guid offerId,
        [FromBody] CarpoolAnswerRequest? request,
        CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        // Kroppen är valfri — ett ja behöver inga ord.
        var answered = await commands
            .SendAsync(
                new AcceptCarpoolRideOfferCommand(offerId, request?.Message, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Answered(answered);
    }

    /// <summary>Den som frågade tackar nej. Meddelandet är obligatoriskt (§KM.12).</summary>
    [HttpPost("ride-offers/{offerId:guid}/deny")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deny(
        Guid offerId,
        CarpoolAnswerRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var answered = await commands
            .SendAsync(
                new DenyCarpoolRideOfferCommand(offerId, request.Message ?? string.Empty, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Answered(answered);
    }

    /// <summary>Föraren återtar sitt platserbjudande.</summary>
    [HttpPost("ride-offers/{offerId:guid}/retract")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retract(Guid offerId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var retracted = await commands
            .SendAsync(new RetractCarpoolRideOfferCommand(offerId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return retracted ? NoContent() : NotFoundForOwner();
    }

    private IActionResult Answered(CarpoolRideAnswerOutcome outcome) => outcome switch
    {
        CarpoolRideAnswerOutcome.Answered => NoContent(),

        CarpoolRideAnswerOutcome.RequestUnavailable => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Förfrågan är inte längre öppen",
            detail: "Den kan redan vara löst eller tillbakadragen."),

        CarpoolRideAnswerOutcome.AlreadyAnswered => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Platserbjudandet är redan besvarat",
            detail: "Skriv till föraren om du har ändrat dig."),

        _ => NotFoundForOffer(),
    };

    private ObjectResult NotFoundForRequest() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Förfrågan går inte att erbjuda plats på",
        detail: "Den kan vara löst eller tillbakadragen.");

    /// <summary>Samma svar för "finns inte" och "tillhör någon annan" — annars går id att kartlägga.</summary>
    private ObjectResult NotFoundForOffer() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Platserbjudandet finns inte",
        detail: "Det kan ha återtagits, eller så är det inte din förfrågan.");

    private ObjectResult NotFoundForOwner() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Finns inte",
        detail: "Det kan redan ha tagits bort, eller så är det inte ditt.");

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
/// Det en förälder fyller i för att be om skjuts. Exakt fälten §KM.12 tillåter: riktning, antal och
/// en valfri rad. Inget namn, inget telefonnummer.
/// </summary>
public sealed record CarpoolRideRequestRequest(CarpoolDirection Direction, int Seats, string? Note)
{
    internal CarpoolRideRequestDraft ToDraft() => new(Direction, Seats, Note);
}

/// <summary>Det en förare fyller i för att erbjuda plats: antal platser och en valfri hälsning.</summary>
public sealed record CarpoolRideOfferRequest(int Seats, string? Message)
{
    internal CarpoolRideOfferDraft ToDraft() => new(Seats, Message);
}
