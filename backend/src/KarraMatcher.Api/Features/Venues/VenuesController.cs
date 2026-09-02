using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Features.Venues;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Venues;

/// <summary>
/// Spelplatsregistret — tränarens uppslagsverk.
///
/// <para>
/// Kräver inloggning, till skillnad från allt annat som handlar om matcher. Spelplatsernas
/// adresser är inte hemliga — de ligger redan i den publika matchlistan — men registret är
/// ett <em>verktyg</em>, och ett sökfält som vem som helst kan hamra på är en onödig yta
/// mot Nominatim.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/venues")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AnyCoach)]
public sealed class VenuesController(VenueRegistry registry) : ControllerBase
{
    /// <summary>Förslag medan tränaren skriver.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<VenueDto>>> Search(
        [FromQuery] string? q,
        CancellationToken cancellationToken) =>
        Ok(await registry.SearchAsync(q ?? string.Empty, cancellationToken).ConfigureAwait(false));

    /// <summary>
    /// Lägger upp en spelplats. Koordinaterna hämtas ur adressen.
    /// </summary>
    /// <remarks>
    /// Kroppen har med flit inga koordinatfält. De härleds server-side, eftersom
    /// handinmatade koordinater visade sig ligga upp till 2,2 km fel — och två kilometer
    /// vid kusten är skillnaden mellan regn och uppehåll i väderprognosen.
    /// </remarks>
    [HttpPost]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        VenueRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await registry
            .CreateAsync(request.Name, request.Address, request.IsHome, cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome switch
        {
            VenueOutcome.Created => Created($"/api/v1/venues/{result.Venue!.Id}", result.Venue),

            VenueOutcome.AddressNotFound => Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Adressen gick inte att hitta",
                detail: "Kontrollera stavningen, eller skriv gatunamn och ort — "
                    + "till exempel \"Klarebergsvallen, Göteborg\"."),

            // 409 med kandidaterna i kroppen: tränaren väljer, och skickar tillbaka den
            // valda adressen. Aldrig dess koordinater — se VenueRegistry.
            VenueOutcome.Ambiguous => Conflict(new AmbiguousAddressResponse(
                "Flera platser matchar adressen. Välj den som stämmer.",
                [.. result.Candidates.Select(c => c.Label)])),

            VenueOutcome.Duplicate => Conflict(new AmbiguousAddressResponse(
                "Det finns redan en spelplats med det namnet.",
                [])),

            _ => Problem(statusCode: StatusCodes.Status500InternalServerError),
        };
    }
}

/// <summary>
/// Det tränaren fyller i om en spelplats.
///
/// <para>
/// <b>Inga koordinatfält.</b> Det är hela poängen: kan klienten skicka en position blir
/// geokodningen en rekommendation i stället för en regel.
/// </para>
/// </summary>
public sealed record VenueRequest(string Name, string Address, bool IsHome);

/// <summary>Adressen matchade flera platser — eller namnet fanns redan.</summary>
public sealed record AmbiguousAddressResponse(string Message, IReadOnlyList<string> Candidates);
