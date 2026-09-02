using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Features.Venues;

/// <summary>Utfallet av ett försök att lägga upp en spelplats.</summary>
public enum VenueOutcome
{
    /// <summary>Sparad.</summary>
    Created,

    /// <summary>Adressen gick inte att hitta. Spelplatsen sparades inte.</summary>
    AddressNotFound,

    /// <summary>Flera platser matchar. Tränaren får välja en av dem.</summary>
    Ambiguous,

    /// <summary>Det finns redan en spelplats med det namnet.</summary>
    Duplicate,
}

/// <summary>Svaret till tränaren: vad som hände, och vad som behövs härnäst.</summary>
public sealed record VenueResult(
    VenueOutcome Outcome,
    VenueDto? Venue,
    IReadOnlyList<GeocodedPlace> Candidates);

/// <summary>
/// Spelplatsregistret.
///
/// <para>
/// <b>Koordinaterna skrivs aldrig in.</b> De härleds ur adressen när spelplatsen sparas,
/// eftersom de sju handinmatade låg upp till 2,2 km fel — och vid kusten är två kilometer
/// skillnaden mellan regn och uppehåll i prognosen (`#110`).
/// </para>
///
/// <para>
/// <b>Klienten skickar aldrig koordinater.</b> Det gäller även bekräftelsesteget: en
/// adress som ger flera träffar besvaras med kandidaterna, och tränaren skickar tillbaka
/// den <em>adress</em> hen valde — inte dess position. Servern slår upp den på nytt. Att
/// ta emot koordinater från en klient hade gjort hela geokodningen till en rekommendation.
/// </para>
/// </summary>
public sealed class VenueRegistry(IVenueRepository venues, IGeocoder geocoder)
{
    /// <summary>Spelplatser som liknar det tränaren skrivit. Underlaget för förslagen.</summary>
    public Task<IReadOnlyList<VenueDto>> SearchAsync(string term, CancellationToken cancellationToken) =>
        venues.SearchAsync(term?.Trim() ?? string.Empty, cancellationToken);

    /// <summary>
    /// Lägger upp en spelplats med koordinater hämtade ur adressen.
    /// </summary>
    public async Task<VenueResult> CreateAsync(
        string name,
        string address,
        bool isHome,
        CancellationToken cancellationToken)
    {
        var trimmedName = name?.Trim() ?? string.Empty;

        if (await venues.ExistsByNameAsync(trimmedName, cancellationToken).ConfigureAwait(false))
        {
            return new VenueResult(VenueOutcome.Duplicate, null, []);
        }

        var hits = await geocoder.LookupAsync(address?.Trim() ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (hits.Count == 0)
        {
            return new VenueResult(VenueOutcome.AddressNotFound, null, []);
        }

        if (hits.Count > 1)
        {
            /*
             * Flera traffar: automatiken far inte valja i tysthet. "Idrottsvagen" finns i
             * varannan kommun, och en gissning har blir en foralder som kor fel.
             *
             * Tranaren skickar tillbaka den valda adressen, inte dess koordinater -- da
             * blir uppslagningen entydig och servern behaller kontrollen over positionen.
             */
            return new VenueResult(VenueOutcome.Ambiguous, null, hits);
        }

        var place = hits[0];

        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,

            // Adressen som leverantören skriver den, inte som tränaren skrev den.
            // Kartlänken bygger på adressen, så den ska vara den som gick att hitta.
            Address = place.Label,
            Latitude = place.Latitude,
            Longitude = place.Longitude,
            IsHome = isHome,
        };

        await venues.AddAsync(venue, cancellationToken).ConfigureAwait(false);
        await venues.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new VenueResult(VenueOutcome.Created, venue.ToDto(), []);
    }
}
