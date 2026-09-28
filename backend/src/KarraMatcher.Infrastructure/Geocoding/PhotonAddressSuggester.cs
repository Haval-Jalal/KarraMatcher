using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using KarraMatcher.Application.Abstractions.Geocoding;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Infrastructure.Geocoding;

/// <summary>
/// Adress-förslag "as-you-type" mot Photon (Komoot, OpenStreetMap-baserad).
///
/// <para>
/// <b>Varför Photon och inte Nominatim.</b> Nominatims villkor tillåter inte autocomplete mot
/// deras publika server; Photon är byggd just för det och kräver varken nyckel eller
/// faktureringskonto. Beslutet att lägga till den som tredjepart är skrivet i
/// <c>docs/PROJEKT-HANDOFF.md</c> (§KM.6).
/// </para>
///
/// <para>
/// <b>SSRF:</b> värden är fast och det skrivna går in som en kodad frågeparameter — aldrig som
/// en URL (checklistan 4.8). Ett värde från en användare kan alltså inte styra <em>vart</em>
/// anropet går, bara vad som frågas efter.
/// </para>
///
/// <para>
/// <b>Fail-soft.</b> En trasig eller långsam uppslagning får aldrig bli ett fel för den som
/// skriver — den ger en tom lista och en varning i loggen. Adressfältet fungerar ändå som
/// fritext, precis som innan förslagen fanns.
/// </para>
///
/// <para>
/// Anropen hålls nere av klienten (debounce, minst tre tecken) och av den globala rate-limitern
/// (§KM.0 A1). Resultaten begränsas till Sverige och biasas mot Göteborgsområdet, där truppen
/// spelar sina matcher.
/// </para>
/// </summary>
internal sealed partial class PhotonAddressSuggester(
    HttpClient http,
    ILogger<PhotonAddressSuggester> logger) : IAddressSuggester
{
    /// <summary>Hur många förslag som hämtas. Fler än så hjälper ingen att välja.</summary>
    private const int MaxResults = 5;

    /// <summary>Kortare än så är inte en sökning värd ett anrop — speglar klientens tröskel.</summary>
    private const int MinTermLength = 3;

    /// <summary>Bias mot Kärra/Göteborg, där lagen spelar. Styr ordningen, inte vad som tas med.</summary>
    private const string LatitudeBias = "57.75";
    private const string LongitudeBias = "11.93";

    public async Task<IReadOnlyList<string>> SuggestAsync(
        string term,
        CancellationToken cancellationToken)
    {
        var trimmed = term?.Trim() ?? string.Empty;

        if (trimmed.Length < MinTermLength)
        {
            return [];
        }

        // Uri.EscapeDataString och inte strängkonkatenering: det skrivna är användarindata.
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"api/?q={Uri.EscapeDataString(trimmed)}&lang=sv&limit={MaxResults}&lat={LatitudeBias}&lon={LongitudeBias}");

        try
        {
            var response = await http.GetFromJsonAsync<PhotonResponse>(query, cancellationToken)
                .ConfigureAwait(false);

            if (response?.Features is null)
            {
                return [];
            }

            return [.. response.Features
                .Where(feature => IsSwedish(feature.Properties))
                .Select(feature => BuildLabel(feature.Properties))
                .Where(label => label is not null)
                .Select(label => label!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxResults)];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // Ett trasigt förslag far inte bli ett fel for den som skriver. Tom lista, och ett
            // spar i loggen — aldrig det skrivna, som ar potentiell PII (§KM.10).
            LogSuggestFailed(logger, ex);

            return [];
        }
    }

    /// <summary>Bara svenska träffar. Photon saknar landsfilter i frågan, så det sker här.</summary>
    private static bool IsSwedish(PhotonProperties? properties) =>
        string.Equals(properties?.CountryCode, "SE", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// En läsbar adressrad ur Photons fält: platsen (namn eller gata + nummer) och orten
    /// (postnummer + stad). Bygger den som en människa skulle skriva den, så den både går att
    /// läsa i listan och att geokoda när platsen sparas.
    /// </summary>
    private static string? BuildLabel(PhotonProperties? p)
    {
        if (p is null)
        {
            return null;
        }

        string? primary;
        if (!string.IsNullOrWhiteSpace(p.Name))
        {
            primary = p.Name.Trim();
        }
        else if (!string.IsNullOrWhiteSpace(p.Street))
        {
            primary = string.IsNullOrWhiteSpace(p.HouseNumber)
                ? p.Street.Trim()
                : $"{p.Street.Trim()} {p.HouseNumber.Trim()}";
        }
        else
        {
            primary = string.IsNullOrWhiteSpace(p.City) ? null : p.City.Trim();
        }

        if (primary is null)
        {
            return null;
        }

        var localityParts = new[] { p.Postcode, p.City }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim());
        var locality = string.Join(" ", localityParts);

        return locality.Length == 0 ? primary : $"{primary}, {locality}";
    }

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Adressförslagen kunde inte hämtas. Fältet fungerar som fritext.")]
    private static partial void LogSuggestFailed(ILogger logger, Exception exception);

    /// <summary>Photons svarsform (GeoJSON). Bara fälten vi använder.</summary>
    private sealed record PhotonResponse
    {
        [JsonPropertyName("features")]
        public IReadOnlyList<PhotonFeature>? Features { get; init; }
    }

    private sealed record PhotonFeature
    {
        [JsonPropertyName("properties")]
        public PhotonProperties? Properties { get; init; }
    }

    private sealed record PhotonProperties
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("street")]
        public string? Street { get; init; }

        [JsonPropertyName("housenumber")]
        public string? HouseNumber { get; init; }

        [JsonPropertyName("postcode")]
        public string? Postcode { get; init; }

        [JsonPropertyName("city")]
        public string? City { get; init; }

        [JsonPropertyName("countrycode")]
        public string? CountryCode { get; init; }
    }
}
