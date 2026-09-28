namespace KarraMatcher.Application.Abstractions.Geocoding;

/// <summary>
/// Föreslår adresser medan en admin skriver, när en aktivitet läggs på en annan plats än
/// klubbens hemmaplan (`#307`, bortamatch/annan plats).
///
/// <para>
/// <b>Bara etiketter, aldrig koordinater.</b> Förslagen är text som fyller i adressfältet —
/// positionen härleds fortfarande server-side när platsen sparas (<see cref="IGeocoder"/>),
/// eftersom klienten aldrig får skicka koordinater. Autocomplete är en <em>skrivhjälp</em>,
/// inte en genväg förbi geokodningen.
/// </para>
///
/// <para>
/// Detta är skilt från <see cref="IGeocoder"/> med flit: geokodningen sker sällan (en gång när
/// en plats sparas) medan förslagen är "as-you-type". De två leverantörerna behöver därför
/// inte vara samma tjänst, och får inte dela strypning eller antaganden.
/// </para>
/// </summary>
public interface IAddressSuggester
{
    /// <summary>
    /// Adress-etiketter som matchar det skrivna, mest sannolika först. Tom lista när inget
    /// hittades eller när uppslagningen misslyckades — ett trasigt förslag får aldrig bli ett fel
    /// för den som skriver.
    /// </summary>
    public Task<IReadOnlyList<string>> SuggestAsync(string term, CancellationToken cancellationToken);
}
