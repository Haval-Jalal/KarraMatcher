namespace KarraMatcher.Application.Abstractions.Geocoding;

/// <summary>En plats som en adressuppslagning hittade.</summary>
/// <param name="Label">Adressen så som leverantören skriver den. Visas för tränaren.</param>
public sealed record GeocodedPlace(string Label, double Latitude, double Longitude);

/// <summary>
/// Slår upp koordinater ur en adress.
///
/// <para>
/// Finns därför att <em>ingen tränare skriver latitud och longitud rätt</em>. De sju
/// spelplatser som skrevs in för hand låg upp till 2,2 km fel, vilket vid kusten är
/// skillnaden mellan regn och uppehåll i väderprognosen (`#110`).
/// </para>
///
/// <para>
/// Uppslagningen är samtidigt en <b>adresskontroll</b>: går adressen inte att hitta får
/// tränaren veta det direkt, i stället för att en förälder upptäcker det en lördagsmorgon
/// när kartappen inte hittar planen.
/// </para>
/// </summary>
public interface IGeocoder
{
    /// <summary>
    /// Platser som matchar adressen, mest sannolika först. Tom lista när inget hittades.
    /// </summary>
    public Task<IReadOnlyList<GeocodedPlace>> LookupAsync(
        string address,
        CancellationToken cancellationToken);
}
