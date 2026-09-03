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
/// Förarens egna erbjudanden: lägga upp och dra tillbaka.
///
/// <para>
/// Kräver konto (§KM.3). Föraren <em>är</em> den inloggade — det finns inget förarfält att
/// skicka, och därmed ingen väg att lägga upp ett erbjudande i någon annans namn.
/// </para>
///
/// <para>
/// Ingen lagpolicy här. Den som kör är en förälder, inte en tränare, och samåkning är öppen
/// för vem som helst med konto. Ägarskapet — som avgör vem som får dra tillbaka vad —
/// prövas i tjänsten, på erbjudandet självt.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/matches/{matchId:guid}/carpool")]
[Produces("application/json")]
[Authorize]
[RequireCsrfToken]
public sealed class CarpoolDriverController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Lägger upp ett erbjudande om skjuts.</summary>
    [HttpPost("offers")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        Guid matchId,
        CarpoolOfferRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var offer = await commands
            .SendAsync(
                new CreateCarpoolOfferCommand(matchId, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return offer is null
            ? Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Matchen finns inte",
                detail: "Kontrollera länken — matchen kan ha tagits bort.")
            : CreatedAtAction(
                actionName: nameof(CarpoolController.List),
                controllerName: "Carpool",
                routeValues: new { matchId },
                value: offer);
    }

    /// <summary>
    /// Drar tillbaka ett erbjudande.
    /// </summary>
    /// <remarks>
    /// Erbjudandet raderas inte. Det blir kvar som tillbakadraget tills gallringen tar hela
    /// matchens samåkning 30 dagar efteråt (§KM.12) — den som frågat ska kunna se vad som
    /// hände med sin förfrågan, inte möta ett tomt hål.
    /// </remarks>
    [HttpPost("offers/{offerId:guid}/withdraw")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(Guid offerId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var withdrawn = await commands
            .SendAsync(new WithdrawCarpoolOfferCommand(offerId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return withdrawn ? NoContent() : NotFoundForOwner();
    }

    /// <summary>
    /// Skickar en förfrågan om att få åka med.
    /// </summary>
    /// <remarks>
    /// <b>Går att skicka även när erbjudandet är fullt.</b> Det är avsiktligt: föraren ska
    /// kunna svara "någon annan hann före" i stället för att den som frågar möts av en död
    /// knapp (§KM.12).
    /// </remarks>
    [HttpPost("offers/{offerId:guid}/requests")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Ask(
        Guid matchId,
        Guid offerId,
        CarpoolRequestRequest request,
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
                new CreateCarpoolRequestCommand(offerId, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            CarpoolRequestOutcome.Created => CreatedAtAction(
                actionName: nameof(Requests),
                routeValues: new { matchId, offerId },
                value: created),

            CarpoolRequestOutcome.AlreadyAsked => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Du har redan frågat",
                detail: "Vänta på svar, eller återta din förfrågan först."),

            CarpoolRequestOutcome.OwnOffer => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Det är ditt eget erbjudande",
                detail: "Du kör ju själv."),

            _ => NotFoundForOffer(),
        };
    }

    /// <summary>
    /// Förfrågningarna på ett erbjudande.
    /// </summary>
    /// <remarks>
    /// Föraren ser alla — det är hen som ska svara. Alla andra ser bara sina egna.
    /// Hälsningen är fritext och får bara nå de inblandade (§KM.12), så filtreringen sitter
    /// i tjänsten och inte i gränssnittet.
    /// </remarks>
    [HttpGet("offers/{offerId:guid}/requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Requests(Guid offerId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var found = await queries
            .SendAsync(new ListCarpoolRequestsQuery(offerId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(found);
    }

    /// <summary>Återtar en egen förfrågan.</summary>
    [HttpPost("requests/{requestId:guid}/retract")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retract(Guid requestId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var retracted = await commands
            .SendAsync(new RetractCarpoolRequestCommand(requestId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return retracted ? NoContent() : NotFoundForOwner();
    }

    private ObjectResult NotFoundForOffer() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Erbjudandet går inte att fråga om",
        detail: "Det kan ha dragits tillbaka.");

    /// <summary>
    /// Samma svar för "finns inte" och "tillhör någon annan".
    ///
    /// <para>
    /// Skulle svaren skilja sig kunde vem som helst räkna ut vilka erbjudande-id som
    /// existerar genom att prova sig fram.
    /// </para>
    /// </summary>
    private ObjectResult NotFoundForOwner() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Erbjudandet finns inte",
        detail: "Det kan redan ha tagits bort.");

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
/// Det föraren fyller i.
///
/// <para>
/// Exakt de fält §KM.12 räknar upp. Inget namn och inget telefonnummer: appen frågar aldrig
/// efter det, och överenskommelsen sker i meddelandefältet.
/// </para>
/// </summary>
/// <summary>
/// Det den som frågar fyller i.
///
/// <para>
/// Standard är en plats. Hälsningen är valfri — den som bara vill fråga ska inte behöva
/// hitta på något att skriva.
/// </para>
/// </summary>
public sealed record CarpoolRequestRequest(int Seats, string? Message)
{
    internal CarpoolRequestDraft ToDraft() => new(Seats, Message);
}

public sealed record CarpoolOfferRequest(
    CarpoolDirection Direction,
    string DeparturePlace,
    DateTime DepartureUtc,
    int Seats,
    string? Note)
{
    internal CarpoolOfferDraft ToDraft() =>
        new(Direction, DeparturePlace, DepartureUtc, Seats, Note);
}
