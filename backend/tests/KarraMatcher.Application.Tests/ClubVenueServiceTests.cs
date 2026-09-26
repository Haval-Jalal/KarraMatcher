using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Clubs;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Klubbens hemmaplan (`#307`).
///
/// <para>
/// Som spelplatsregistret: koordinaterna skrivs aldrig in, de härleds ur adressen. En adress
/// som inte hittas avvisas, och en tvetydig adress låter tränaren välja i stället för att
/// gissa. Hemmaplanen bor på klubben och sätts via en av dess truppar.
/// </para>
/// </summary>
public sealed class ClubVenueServiceTests
{
    private readonly Guid _truppId = Guid.NewGuid();
    private readonly Club _club = new() { Id = Guid.NewGuid(), Name = "Kärra", Slug = "karra" };

    private ClubVenueService CreateService(params GeocodedPlace[] hits) =>
        new(new FakeClubVenueRepository(_truppId, _club), new StubGeocoder(hits), new NoOpAuditLog());

    [Fact]
    public async Task Set_TarKoordinaterFranAdressen()
    {
        var service = CreateService(new GeocodedPlace("Klarebergsvallen, Göteborg", 57.7845, 11.9612));

        var result = await service.SetByTruppAsync(
            _truppId, "Klarebergsvallen", "Klarebergsvallen", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(SetClubVenueOutcome.Set, result.Outcome);
        Assert.Equal("Klarebergsvallen", _club.HomeVenueName);
        Assert.Equal("Klarebergsvallen, Göteborg", _club.HomeAddress);
        Assert.Equal(57.7845, _club.HomeLatitude);
        Assert.Equal(11.9612, _club.HomeLongitude);
    }

    [Fact]
    public async Task Set_AdressSomInteHittas_Avvisas()
    {
        var service = CreateService();

        var result = await service.SetByTruppAsync(
            _truppId, "Planen", "Finns inte", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(SetClubVenueOutcome.AddressNotFound, result.Outcome);
        Assert.Null(_club.HomeLatitude);
    }

    [Fact]
    public async Task Set_FlertydigAdress_LaterTranarenValja()
    {
        var service = CreateService(
            new GeocodedPlace("Idrottsvägen, Göteborg", 57.7, 11.9),
            new GeocodedPlace("Idrottsvägen, Kungälv", 57.8, 11.9));

        var result = await service.SetByTruppAsync(
            _truppId, "Planen", "Idrottsvägen", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(SetClubVenueOutcome.Ambiguous, result.Outcome);
        Assert.Equal(2, result.Candidates.Count);
        Assert.Null(_club.HomeLatitude);
    }

    [Fact]
    public async Task Set_OkandTrupp_GerTruppNotFound()
    {
        var service = new ClubVenueService(
            new FakeClubVenueRepository(_truppId, null), new StubGeocoder([]), new NoOpAuditLog());

        var result = await service.SetByTruppAsync(
            Guid.NewGuid(), "Planen", "Adress", Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(SetClubVenueOutcome.TruppNotFound, result.Outcome);
    }

    [Fact]
    public async Task Get_SpeglarKlubbensHemmaplan()
    {
        _club.HomeVenueName = "Klarebergsvallen";
        _club.HomeAddress = "Klarebergsvallen, Göteborg";
        _club.HomeLatitude = 57.78;
        _club.HomeLongitude = 11.96;
        var service = CreateService();

        var dto = await service.GetByTruppAsync(_truppId, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.True(dto!.Configured);
        Assert.Equal("Klarebergsvallen", dto.Name);
        Assert.Equal(57.78, dto.Latitude);
    }

    private sealed class FakeClubVenueRepository(Guid truppId, Club? club) : IClubVenueRepository
    {
        public Task<Club?> FindClubByTruppAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == truppId ? club : null);

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class StubGeocoder(GeocodedPlace[] hits) : IGeocoder
    {
        public Task<IReadOnlyList<GeocodedPlace>> LookupAsync(
            string address, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GeocodedPlace>>(hits);
    }

    private sealed class NoOpAuditLog : IAuditLog
    {
        public Task RecordAsync(
            string action,
            Guid actorAccountId,
            CancellationToken cancellationToken,
            Guid? subjectId = null,
            string? details = null) => Task.CompletedTask;
    }
}
