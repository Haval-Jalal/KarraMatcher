using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Clubs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Clubs;

/// <summary>
/// Truppens hemmaplan (`#307`, flyttad till truppen i `#405`). Nås via en trupp och vaktas av
/// <c>AdminOfTrupp</c>, så en admin för <em>just den truppen</em> sätter den — och planen gäller
/// bara den truppen, inte hela klubben. En annan trupp i samma klubb sätter sin egen (ingen
/// delning mellan truppar, §KM.3). Adressen geokodas server-side så att väder och vägbeskrivning
/// har koordinater.
///
/// <para>
/// <b>Namnen är historiska.</b> Route (<c>club-venue</c>), DTO:er och <c>…ClubVenue…</c>-namnen
/// behölls vid flytten för liten diff (se `#405`-beslutet i docs/PROJEKT-HANDOFF.md), men lagras
/// och läses per trupp (<c>ClubVenueService</c> skriver till <c>AgeGroup</c>). Återinför aldrig
/// klubb-delning på det här namnet.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/club-venue")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
public sealed class ClubVenueController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : AdminControllerBase
{
    /// <summary>Truppens nuvarande hemmaplan (namn, adress, koordinater), eller "inte satt".</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClubVenueDto>> Get(Guid truppId, CancellationToken cancellationToken)
    {
        var venue = await queries
            .SendAsync(new GetClubVenueQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return venue is null ? NotFoundTrupp() : Ok(venue);
    }

    /// <summary>Sätter (eller ändrar) truppens hemmaplan. Geokodar adressen.</summary>
    [HttpPut]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Set(
        Guid truppId,
        ClubVenueRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new SetClubVenueCommand(
                    truppId, request.Name ?? string.Empty, request.Address ?? string.Empty, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome switch
        {
            SetClubVenueOutcome.Set => NoContent(),

            SetClubVenueOutcome.AddressNotFound => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Adressen gick inte att hitta",
                detail: "Kontrollera stavningen, eller skriv gatunamn och ort — "
                    + "till exempel \"Klarebergsvallen, Göteborg\"."),

            SetClubVenueOutcome.Ambiguous => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Flera platser matchar adressen",
                detail: "Skriv adressen mer exakt — gatunamn och ort, "
                    + "till exempel \"Klarebergsvallen, Göteborg\"."),

            _ => NotFoundTrupp(),
        };
    }

    private ObjectResult NotFoundTrupp() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Truppen finns inte",
        detail: "Kontrollera länken — truppen kan ha tagits bort.");
}

/// <summary>Det tränaren fyller i om klubbens hemmaplan: namn och adress (koordinater geokodas).</summary>
public sealed record ClubVenueRequest(string? Name, string? Address);
