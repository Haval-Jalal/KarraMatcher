using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Geocoding;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Venues;

/// <summary>
/// Adress-förslag medan en admin skriver in en annan plats än hemmaplanen (`#307`).
///
/// <para>
/// Kräver inloggning som tränare/admin (<see cref="AuthorizationPolicies.AnyCoach"/>): bara den
/// som får skapa aktiviteter behöver förslagen, och ett öppet sökfält vore en onödig yta mot
/// tredjeparten. Föräldrar rör aldrig det här — de öppnar vägbeskrivningen inne på aktiviteten,
/// som är en ren kartlänk.
/// </para>
///
/// <para>
/// Endpointen returnerar bara adress-<em>etiketter</em>. Positionen härleds fortfarande
/// server-side när platsen sparas — klienten skickar aldrig koordinater. Anropen skyddas av den
/// globala rate-limitern (§KM.0 A1); klienten debouncar och kräver minst tre tecken.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/address-suggestions")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AnyCoach)]
public sealed class AddressSuggestionsController(IAddressSuggester suggester) : ControllerBase
{
    /// <summary>Förslag som matchar det skrivna. Tom lista när inget hittas.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> Suggest(
        [FromQuery] string? q,
        CancellationToken cancellationToken) =>
        Ok(await suggester.SuggestAsync(q ?? string.Empty, cancellationToken).ConfigureAwait(false));
}
