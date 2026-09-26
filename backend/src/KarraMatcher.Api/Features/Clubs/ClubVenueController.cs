using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Clubs;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Clubs;

/// <summary>
/// Klubbens hemmaplan (`#307`). Nås via en trupp och vaktas av <c>AdminOfTrupp</c>, så vilken
/// tränare/admin som helst i klubben kan sätta den — planen bor på klubben och delas av alla
/// dess truppar. Adressen geokodas server-side så att väder och vägbeskrivning har koordinater.
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/club-venue")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
public sealed class ClubVenueController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : AdminControllerBase
{
    /// <summary>Klubbens nuvarande hemmaplan (namn, adress, koordinater), eller "inte satt".</summary>
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

    /// <summary>Sätter (eller ändrar) klubbens hemmaplan. Geokodar adressen.</summary>
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
