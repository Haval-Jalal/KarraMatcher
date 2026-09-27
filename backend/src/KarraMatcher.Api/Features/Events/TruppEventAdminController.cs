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

    /// <summary>
    /// Ändrar en händelse i truppen — trupp-vid eller lag-riktad. Laget flyttas inte här
    /// (<c>TeamId</c> i kroppen ignoreras vid ändring); det bestäms när händelsen skapas.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid truppId,
        Guid id,
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
                new UpdateTruppEventCommand(truppId, id, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome == EventSaveOutcome.Ok ? Ok(result.Event) : ProblemFor(result.Outcome);
    }

    /// <summary>Ställer in en händelse — den blir kvar i kalendern, markerad som inställd.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var item = await dispatcher
            .SendAsync(new CancelTruppEventCommand(truppId, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return item is null ? NotFoundForTrupp() : Ok(item);
    }

    /// <summary>Tar bort en händelse som aldrig skulle ha lagts in.</summary>
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

        var removed = await dispatcher
            .SendAsync(new DeleteTruppEventCommand(truppId, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return removed ? NoContent() : NotFoundForTrupp();
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

    /// <summary>
    /// Samma svar för "finns inte" och "hör till en annan trupp" — annars kunde en admin prova
    /// sig fram till vilka händelse-id som finns i andra truppar (jfr <c>EventAdminController</c>).
    /// </summary>
    private ObjectResult NotFoundForTrupp() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Händelsen finns inte",
        detail: "Kontrollera länken — händelsen kan ha tagits bort.");

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
