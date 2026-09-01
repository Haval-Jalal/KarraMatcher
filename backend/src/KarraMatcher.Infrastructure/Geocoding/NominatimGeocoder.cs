using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using KarraMatcher.Application.Abstractions.Geocoding;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Infrastructure.Geocoding;

/// <summary>
/// Adressuppslagning mot Nominatim (OpenStreetMap).
///
/// <para>
/// <b>SSRF:</b> värden är fast och adressen går in som en kodad frågeparameter — aldrig
/// som en URL (checklistan 4.8). Ett värde från en användare kan alltså inte styra
/// <em>vart</em> anropet går, bara vad som frågas efter.
/// </para>
///
/// <para>
/// <b>Anropen är få med flit.</b> Uppslagningen sker en gång, när spelplatsen sparas, och
/// resultatet lagras. Aldrig vid läsning. En handfull anrop per säsong ligger med god
/// marginal inom Nominatims villkor om högst en förfrågan per sekund, och det är också
/// varför ingen egen strypning behövs.
/// </para>
///
/// <para>
/// Villkoren kräver dessutom att anroparen går att identifiera. Därför en <c>User-Agent</c>
/// som säger vad det här är och hur man når oss — att skicka anonymt vore att bryta mot
/// villkoren för en tjänst som drivs av frivilliga.
/// </para>
/// </summary>
internal sealed partial class NominatimGeocoder(
    HttpClient http,
    ILogger<NominatimGeocoder> logger) : IGeocoder
{
    /// <summary>Hur många träffar som hämtas. Fler än så hjälper ingen att välja.</summary>
    private const int MaxResults = 5;

    public async Task<IReadOnlyList<GeocodedPlace>> LookupAsync(
        string address,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return [];
        }

        // Uri.EscapeDataString och inte strangkonkatenering: adressen ar anvandarindata.
        var query = string.Create(
            CultureInfo.InvariantCulture,
            $"search?format=jsonv2&limit={MaxResults}&countrycodes=se&q={Uri.EscapeDataString(address)}");

        try
        {
            var hits = await http.GetFromJsonAsync<NominatimHit[]>(query, cancellationToken)
                .ConfigureAwait(false);

            return hits is null
                ? []
                : [.. hits
                    .Where(hit => hit.DisplayName is not null)
                    .Select(hit => new GeocodedPlace(
                        hit.DisplayName!,
                        double.Parse(hit.Latitude ?? "0", CultureInfo.InvariantCulture),
                        double.Parse(hit.Longitude ?? "0", CultureInfo.InvariantCulture)))];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or FormatException)
        {
            // En trasig uppslagning far inte bli ett 500 for tranaren. Anroparen far en
            // tom lista och ett begripligt besked om att adressen inte gick att hitta.
            LogLookupFailed(logger, ex);

            return [];
        }
    }

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Adressuppslagningen misslyckades. Ingen spelplats kunde geokodas.")]
    private static partial void LogLookupFailed(ILogger logger, Exception exception);

    /// <summary>Nominatims svarsform. Koordinaterna kommer som strängar.</summary>
    private sealed record NominatimHit
    {
        [JsonPropertyName("display_name")]
        public string? DisplayName { get; init; }

        [JsonPropertyName("lat")]
        public string? Latitude { get; init; }

        [JsonPropertyName("lon")]
        public string? Longitude { get; init; }
    }
}
