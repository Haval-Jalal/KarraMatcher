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
public sealed class CarpoolDriverController(ICommandDispatcher commands) : ControllerBase
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
