using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Events.Admin;
using KarraMatcher.Application.Features.Events.Import;
using KarraMatcher.Domain.Events;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Events;

/// <summary>
/// Tränarens händelsehantering — matcher, träningar och övrigt (`#198`).
///
/// <para>
/// <b>Laget står i adressen, och det är avsiktligt.</b> Policyn <c>CoachOfTeam</c> prövar
/// behörigheten mot just den slugen, så en tränare för Gul kan inte nå Blås händelser genom
/// att skicka ett annat lag i kroppen — det finns inget lagfält att skicka.
/// </para>
///
/// <para>
/// Skild från <c>EventsController</c>: den ena läser och den andra skriver och kräver rätt
/// tränare. Att blanda dem i samma klass är hur en skrivning råkar ärva en läsnings öppenhet.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}/events")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
[RequireCsrfToken]
public sealed class EventAdminController(
    ICommandDispatcher dispatcher,
    ScheduleImportService import) : ControllerBase
{
    /// <summary>Lägger upp en händelse i laget.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        string slug,
        EventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var item = await dispatcher
            .SendAsync(new CreateEventCommand(slug, request.ToDraft(), actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return item is null
            ? Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Händelsen gick inte att lägga upp",
                detail: "Kontrollera att laget och spelplatsen finns.")
            : CreatedAtAction(
                actionName: nameof(EventsController.GetEvent),
                controllerName: "Events",
                routeValues: new { id = item.Id },
                value: item);
    }

    /// <summary>Ändrar en händelse.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string slug,
        Guid id,
        EventRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var item = await dispatcher
            .SendAsync(
                new UpdateEventCommand(slug, id, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return item is null ? NotFoundForTeam() : Ok(item);
    }

    /// <summary>Ställer in en händelse — den blir kvar i kalendern, markerad som inställd.</summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(string slug, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var item = await dispatcher
            .SendAsync(new CancelEventCommand(slug, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return item is null ? NotFoundForTeam() : Ok(item);
    }

    /// <summary>Tar bort en händelse som aldrig skulle ha lagts in.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string slug, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var removed = await dispatcher
            .SendAsync(new DeleteEventCommand(slug, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return removed ? NoContent() : NotFoundForTeam();
    }

    /// <summary>Tolkar en inklistring utan att spara något (matchschema).</summary>
    /// <remarks>
    /// Ingen ska behöva lita på en parser i blindo. Förhandsgranskningen är det som gör
    /// massinlägget tryggt nog att faktiskt användas.
    /// </remarks>
    [HttpPost("import/preview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ImportResult>> Preview(
        string slug,
        ImportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Ok(await import.PreviewAsync(slug, request.Text, cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>Sparar de rader som gick igenom.</summary>
    /// <remarks>
    /// <b>Texten tolkas om här.</b> Klienten skickar samma inklistring en gång till, aldrig
    /// den tolkade listan — annars vore förhandsgranskningen en rekommendation.
    /// </remarks>
    [HttpPost("import")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(
        string slug,
        ImportRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        return Ok(await import
            .ImportAsync(slug, request.Text, actor.Value, cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>
    /// Samma svar för "finns inte" och "hör till ett annat lag".
    ///
    /// <para>
    /// Skulle svaren skilja sig kunde en tränare för Gul räkna ut vilka id som finns i Blå
    /// genom att prova sig fram.
    /// </para>
    /// </summary>
    private ObjectResult NotFoundForTeam() => Problem(
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
/// Det tränaren fyller i om en händelse.
///
/// <para>
/// Inget lagfält: laget kommer ur adressen, som är det behörigheten prövas mot. Typen är en
/// sträng ("Match", "Training", "Other") — okänt värde fångas av valideringen.
/// </para>
/// </summary>
public sealed record EventRequest(
    string Type,
    DateTime KickoffUtc,
    string? Title,
    string? Opponent,
    Guid VenueId,
    bool? IsHome,
    string? AddressOverride,
    string? Note)
{
    internal EventDraft ToDraft() =>
        new(
            Enum.TryParse<EventType>(Type, ignoreCase: true, out var type) ? type : (EventType)(-1),
            KickoffUtc,
            Title,
            Opponent,
            VenueId,
            IsHome,
            AddressOverride,
            Note);
}

/// <summary>Den inklistrade texten. Skickas oförändrad både till granskning och import.</summary>
public sealed record ImportRequest(string? Text);
