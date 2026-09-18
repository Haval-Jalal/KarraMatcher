using KarraMatcher.Application.Features.Events.GetEvent;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Tests;

public class GetEventQueryHandlerTests
{
    private static readonly DateTime Kickoff = new(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

    private static Event NewMatch(string? addressOverride = null, EventStatus status = EventStatus.Scheduled)
    {
        var ageGroup = FakeTeamRepository.NewAgeGroup();

        return new Event
        {
            Id = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            Team = new Team
            {
                Id = Guid.NewGuid(),
                AgeGroupId = ageGroup.Id,
                AgeGroup = ageGroup,
                Name = "Gul",
                ColorHex = "#D9A21B",
                Slug = "gul",
            },
            KickoffUtc = Kickoff,
            OpponentName = "Motstandarna",
            VenueId = Guid.NewGuid(),
            Venue = FakeTeamRepository.NewVenue(),
            AddressOverride = addressOverride,
            IsHome = true,
            Status = status,
        };
    }

    [Fact]
    public async Task HandleAsync_OkantId_GerNull()
    {
        // En okänd match är en gammal länk, inte ett systemfel — en förälder kan mycket
        // väl öppna en kalenderpost från förra säsongen.
        var handler = new GetEventQueryHandler(new FakeEventRepository());

        var result = await handler.HandleAsync(new GetEventQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_KandMatch_GerMatchenOchLaget()
    {
        var repository = new FakeEventRepository();
        var match = NewMatch();
        repository.Matches.Add(match);
        var handler = new GetEventQueryHandler(repository);

        var result = await handler.HandleAsync(new GetEventQuery(match.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(match.Id, result.Event.Id);
        Assert.Equal("gul", result.Team.Slug);
        Assert.Equal("P2016", result.Team.AgeGroup);
    }

    [Fact]
    public async Task HandleAsync_GerKoordinaterFranSpelplatsen()
    {
        // Koordinaterna driver väderprognosen. De ska komma ur vår egen Venue-tabell och
        // aldrig från något anroparen skickat in (SSRF-regeln i CLAUDE.md).
        var repository = new FakeEventRepository();
        var match = NewMatch();
        repository.Matches.Add(match);
        var handler = new GetEventQueryHandler(repository);

        var result = await handler.HandleAsync(new GetEventQuery(match.Id), CancellationToken.None);

        Assert.Equal(57.79, result!.Event.Venue.Latitude);
        Assert.Equal(11.94, result.Event.Venue.Longitude);
    }

    [Fact]
    public async Task HandleAsync_AvvikandeAdress_VinnerOverSpelplatsens()
    {
        var repository = new FakeEventRepository();
        var match = NewMatch(addressOverride: "Bortavagen 9, Molndal");
        repository.Matches.Add(match);
        var handler = new GetEventQueryHandler(repository);

        var result = await handler.HandleAsync(new GetEventQuery(match.Id), CancellationToken.None);

        Assert.Equal("Bortavagen 9, Molndal", result!.Event.Address);
        Assert.Equal("Idrottsvagen 1, Goteborg", result.Event.Venue.Address);
    }

    [Fact]
    public async Task HandleAsync_InstalldMatch_ArMarkt()
    {
        var repository = new FakeEventRepository();
        var match = NewMatch(status: EventStatus.Cancelled);
        repository.Matches.Add(match);
        var handler = new GetEventQueryHandler(repository);

        var result = await handler.HandleAsync(new GetEventQuery(match.Id), CancellationToken.None);

        Assert.Equal("Cancelled", result!.Event.Status);
    }

    [Fact]
    public async Task HandleAsync_MatchUtanLag_GerNull()
    {
        // Ska inte kunna hända -- främmande nyckeln är obligatorisk -- men ett null som
        // slinker igenom hade blivit ett 500 hos anroparen i stället för ett 404.
        var repository = new FakeEventRepository();
        var match = NewMatch();
        match.Team = null;
        repository.Matches.Add(match);
        var handler = new GetEventQueryHandler(repository);

        var result = await handler.HandleAsync(new GetEventQuery(match.Id), CancellationToken.None);

        Assert.Null(result);
    }
}
