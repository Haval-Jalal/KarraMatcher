using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Events.Admin;
using KarraMatcher.Domain.Events;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Events;

/// <summary>
/// Adminens händelsehantering på trupp-nivå (`#332`).
///
/// <para>
/// Till skillnad från <see cref="EventAdminController"/> — som riktar en tränare mot ett
/// enskilt lag via slugen — arbetar den här på hela truppen. Behörigheten prövas mot
/// <c>{truppId}</c> med <c>AdminOfTrupp</c>, och laget är valfritt i kroppen: utelämnas det
/// (eller skickas <c>null</c>) blir händelsen <b>trupp-vid</b> (hela truppen). Ett angivet lag
/// måste höra till truppen — det vaktas server-side, inte bara av att fältet finns.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/events")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class TruppEventAdminController(ICommandDispatcher dispatcher) : ControllerBase
{
    /// <summary>Lägger upp en händelse i truppen — trupp-vid, eller riktad mot ett av truppens lag.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        Guid truppId,
        AdminEventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await dispatcher
            .SendAsync(
                new CreateTruppEventCommand(truppId, request.TeamId, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome == EventSaveOutcome.Ok
            ? CreatedAtAction(
                actionName: nameof(EventsController.GetEvent),
                controllerName: "Events",
                routeValues: new { id = result.Event!.Id },
                value: result.Event)
            : ProblemFor(result.Outcome);
    }

    private ObjectResult ProblemFor(EventSaveOutcome outcome) => outcome switch
    {
        EventSaveOutcome.NoHomeVenue => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Klubben har ingen hemmaplan",
            detail: "Sätt klubbens hemmaplan under Inställningar innan du lägger upp en hemma-aktivitet."),

        EventSaveOutcome.AddressNotFound => Problem(
            statusCode: StatusCodes.Status422UnprocessableEntity,
            title: "Adressen gick inte att hitta",
            detail: "Kontrollera stavningen, eller skriv gatunamn och ort — "
                + "till exempel \"Klarebergsvallen, Göteborg\"."),

        EventSaveOutcome.AddressAmbiguous => Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Flera platser matchar adressen",
            detail: "Skriv adressen mer exakt — gatunamn och ort."),

        // TeamNotFound (och allt oväntat): truppen eller det angivna laget finns inte i truppen.
        _ => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Truppen eller laget finns inte",
            detail: "Kontrollera att laget hör till truppen."),
    };

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
/// Det en admin fyller i om en trupp-vid eller lag-riktad händelse. Samma fält som
/// <c>EventRequest</c>, plus ett valfritt <see cref="TeamId"/>: utelämnat = hela truppen.
/// </summary>
public sealed record AdminEventRequest(
    string Type,
    DateTime KickoffUtc,
    string? Title,
    string? Opponent,
    bool? IsHome,
    string? Address,
    string? Note,
    Guid? TeamId)
{
    internal EventDraft ToDraft() =>
        new(
            Enum.TryParse<EventType>(Type, ignoreCase: true, out var type) ? type : (EventType)(-1),
            KickoffUtc,
            Title,
            Opponent,
            IsHome,
            Address,
            Note);
}
