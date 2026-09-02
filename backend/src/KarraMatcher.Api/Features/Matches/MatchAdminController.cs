using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Matches;
using KarraMatcher.Application.Features.Matches.Admin;
using KarraMatcher.Application.Features.Matches.Import;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Matches;

/// <summary>
/// Tränarens matchhantering.
///
/// <para>
/// <b>Laget står i adressen, och det är avsiktligt.</b> Policyn <c>CoachOfTeam</c> prövar
/// behörigheten mot just den slugen, så en tränare för Gul kan inte nå Blås matcher genom
/// att skicka ett annat lag i kroppen — det finns inget lagfält att skicka.
/// </para>
///
/// <para>
/// Skild från den publika <c>MatchesController</c>: den ena läser och är öppen för alla,
/// den andra skriver och kräver rätt tränare. Att blanda dem i samma klass är hur en
/// skrivning råkar ärva en läsnings öppenhet.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}/matches")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
[RequireCsrfToken]
public sealed class MatchAdminController(
    ICommandDispatcher dispatcher,
    ScheduleImportService import) : ControllerBase
{
    /// <summary>Lägger upp en match i laget.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        string slug,
        MatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var match = await dispatcher
            .SendAsync(new CreateMatchCommand(slug, request.ToDraft(), actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return match is null
            ? Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Matchen gick inte att lägga upp",
                detail: "Kontrollera att laget och spelplatsen finns.")
            : CreatedAtAction(
                actionName: nameof(MatchesController.GetMatch),
                controllerName: "Matches",
                routeValues: new { id = match.Id },
                value: match);
    }

    /// <summary>Ändrar en match.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string slug,
        Guid id,
        MatchRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var match = await dispatcher
            .SendAsync(
                new UpdateMatchCommand(slug, id, request.ToDraft(), actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return match is null ? NotFoundForTeam() : Ok(match);
    }

    /// <summary>Ställer in en match — den blir kvar i kalendern, markerad som inställd.</summary>
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

        var match = await dispatcher
            .SendAsync(new CancelMatchCommand(slug, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return match is null ? NotFoundForTeam() : Ok(match);
    }

    /// <summary>Tar bort en match som aldrig skulle ha lagts in.</summary>
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
            .SendAsync(new DeleteMatchCommand(slug, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return removed ? NoContent() : NotFoundForTeam();
    }

    /// <summary>Tolkar en inklistring utan att spara något.</summary>
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
    /// den tolkade listan — annars vore förhandsgranskningen en rekommendation, och en
    /// tränare kunde skicka in vad som helst som "det parsern kom fram till".
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
    /// Skulle svaren skilja sig kunde en tränare för Gul räkna ut vilka match-id som finns
    /// i Blå genom att prova sig fram.
    /// </para>
    /// </summary>
    private ObjectResult NotFoundForTeam() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Matchen finns inte",
        detail: "Kontrollera länken — matchen kan ha tagits bort.");

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
/// Det tränaren fyller i.
///
/// <para>
/// Inget lagfält: laget kommer ur adressen, som är det behörigheten prövas mot. Ett
/// lagfält här hade varit en väg förbi hela kontrollen.
/// </para>
/// </summary>
public sealed record MatchRequest(
    DateTime KickoffUtc,
    string Opponent,
    Guid VenueId,
    bool IsHome,
    string? AddressOverride,
    string? Note)
{
    internal MatchDraft ToDraft() =>
        new(KickoffUtc, Opponent, VenueId, IsHome, AddressOverride, Note);
}

/// <summary>Den inklistrade texten. Skickas oförändrad både till granskning och import.</summary>
public sealed record ImportRequest(string? Text);
