using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Venues;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Spelplatsregistret (`#37`).
///
/// <para>
/// Kärnan: <b>koordinater skrivs aldrig in, de härleds ur adressen.</b> De sju
/// handinmatade spelplatserna låg upp till 2,2 km fel, och vid kusten är två kilometer
/// skillnaden mellan regn och uppehåll i väderprognosen. Ingen tränare skriver latitud och
/// longitud rätt, så fältet finns inte att skriva i.
/// </para>
///
/// <para>
/// Uppslagningen är samtidigt en adresskontroll. En adress som inte går att hitta avvisas
/// direkt — hellre det än att en förälder upptäcker det en lördagsmorgon när kartappen
/// inte hittar planen.
/// </para>
/// </summary>
public sealed class VenueRegistryTests
{
    private readonly FakeVenueRepository _venues = new();

    private VenueRegistry CreateRegistry(params GeocodedPlace[] hits) =>
        new(_venues, new StubGeocoder(hits));

    [Fact]
    public async Task Skapa_TarKoordinaterFranAdressen()
    {
        var registry = CreateRegistry(new GeocodedPlace("Klarebergsvallen, Göteborg", 57.7845, 11.9612));

        var result = await registry.CreateAsync(
            "Klarebergsvallen", "Klarebergsvallen 3", isHome: true, CancellationToken.None);

        Assert.Equal(VenueOutcome.Created, result.Outcome);
        Assert.Equal(57.7845, result.Venue!.Latitude);
        Assert.Equal(11.9612, result.Venue.Longitude);
    }

    [Fact]
    public async Task Skapa_SparaLeverantorensAdressOchInteTranarensStavning()
    {
        // Kartlänken bygger på adressen. Den ska vara den som faktiskt gick att hitta —
        // annars leder länken till samma stavfel som uppslagningen redan rättat.
        var registry = CreateRegistry(new GeocodedPlace("Klarebergsvallen, 425 36 Göteborg", 57.78, 11.96));

        var result = await registry.CreateAsync(
            "Klarebergsvallen", "klarebergsvalen 3", isHome: true, CancellationToken.None);

        Assert.Equal("Klarebergsvallen, 425 36 Göteborg", result.Venue!.Address);
    }

    [Fact]
    public async Task Skapa_AdressSomInteGarAttHitta_Avvisas()
    {
        // Tyst nolla vore det sämsta: vädret hade hämtats för Nollön i Guineabukten, och
        // kartlänken hade lett mitt ut i Atlanten.
        var registry = CreateRegistry();

        var result = await registry.CreateAsync(
            "Hittepåplan", "Finns inte alls", isHome: false, CancellationToken.None);

        Assert.Equal(VenueOutcome.AddressNotFound, result.Outcome);
        Assert.Null(result.Venue);
        Assert.Empty(_venues.Venues);
    }

    [Fact]
    public async Task Skapa_FleraTraffar_LaterTranarenValja()
    {
        /*
         * "Idrottsvagen" finns i varannan kommun. Att automatiken valde den forsta hade
         * varit ett tyst fel som slutar med att en foralder kor till fel ort.
         */
        var registry = CreateRegistry(
            new GeocodedPlace("Idrottsvägen, Göteborg", 57.7, 11.9),
            new GeocodedPlace("Idrottsvägen, Kungälv", 57.8, 11.9));

        var result = await registry.CreateAsync(
            "Idrottsplatsen", "Idrottsvägen", isHome: false, CancellationToken.None);

        Assert.Equal(VenueOutcome.Ambiguous, result.Outcome);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Empty(_venues.Venues);
    }

    [Fact]
    public async Task Skapa_SammaNamnTvaGanger_Avvisas()
    {
        // Två "Karra IP" i förslagslistan är värre än inget förslag alls.
        var registry = CreateRegistry(new GeocodedPlace("Karra IP", 57.79, 11.94));

        await registry.CreateAsync("Karra IP", "Idrottsvagen 1", true, CancellationToken.None);
        var second = await registry.CreateAsync(
            "Karra IP", "Idrottsvagen 1", true, CancellationToken.None);

        Assert.Equal(VenueOutcome.Duplicate, second.Outcome);
        Assert.Single(_venues.Venues);
    }

    [Fact]
    public async Task Sok_GerForslagFranRegistret()
    {
        var registry = CreateRegistry(new GeocodedPlace("Karra IP", 57.79, 11.94));
        await registry.CreateAsync("Karra IP", "Idrottsvagen 1", true, CancellationToken.None);

        var hits = await registry.SearchAsync("karra", CancellationToken.None);

        Assert.Single(hits);
    }

    /// <summary>Svarar med det testet bestämt. Nätet är aldrig inblandat.</summary>
    private sealed class StubGeocoder(GeocodedPlace[] hits) : IGeocoder
    {
        public Task<IReadOnlyList<GeocodedPlace>> LookupAsync(
            string address,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GeocodedPlace>>(hits);
    }

    private sealed class FakeVenueRepository : IVenueRepository
    {
        public List<Venue> Venues { get; } = [];

        public Task<IReadOnlyList<VenueDto>> SearchAsync(
            string term,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<VenueDto>>(
                [.. Venues
                    .Where(v => term.Length == 0
                        || v.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || v.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
                    .Select(v => new VenueDto(v.Id, v.Name, v.Address, v.Latitude, v.Longitude, v.IsHome))]);

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Venues.Exists(v => v.Name == name));

        public Task AddAsync(Venue venue, CancellationToken cancellationToken)
        {
            Venues.Add(venue);

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
