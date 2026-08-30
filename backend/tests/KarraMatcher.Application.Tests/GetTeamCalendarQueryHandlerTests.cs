using KarraMatcher.Application.Features.Calendar.GetTeamCalendar;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Kalenderfeeden är sannolikt appens mest värdefulla funktion: föräldern prenumererar en
/// gång och slipper sedan öppna appen. Det gör också att fel här är långlivade — en
/// prenumeration ligger kvar i en telefon i åratal.
/// </summary>
public class GetTeamCalendarQueryHandlerTests
{
    private static readonly DateTime Kickoff = new(2026, 8, 30, 11, 15, 0, DateTimeKind.Utc);
    private static readonly DateTime Updated = new(2026, 8, 25, 6, 0, 0, DateTimeKind.Utc);

    private static (GetTeamCalendarQueryHandler Handler, FakeTeamRepository Repository) Build()
    {
        var repository = new FakeTeamRepository();

        return (new GetTeamCalendarQueryHandler(repository), repository);
    }

    private static async Task<string> FeedForAsync(Action<FakeTeamRepository> arrange)
    {
        var (handler, repository) = Build();
        arrange(repository);

        var result = await handler.HandleAsync(new GetTeamCalendarQuery("gul"), CancellationToken.None);

        Assert.NotNull(result);
        return result;
    }

    [Fact]
    public async Task HandleAsync_OkantLag_GerNull()
    {
        var (handler, _) = Build();

        var result = await handler.HandleAsync(
            new GetTeamCalendarQuery("finns-inte"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_LagUtanMatcher_GerEnGiltigTomKalender()
    {
        // En nystartad säsong har inga matcher inlagda än. Feeden måste ändå gå att
        // prenumerera på, annars måste föräldern komma ihåg att göra om det senare.
        var feed = await FeedForAsync(repository => repository.AddTeam("gul", "Gul"));

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", feed, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", feed, StringComparison.Ordinal);
        Assert.DoesNotContain("BEGIN:VEVENT", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_HarObligatoriskaKalenderfalt()
    {
        var feed = await FeedForAsync(repository => repository.AddTeam("gul", "Gul"));

        Assert.Contains("VERSION:2.0", feed, StringComparison.Ordinal);
        Assert.Contains("PRODID:", feed, StringComparison.Ordinal);
        Assert.Contains("CALSCALE:GREGORIAN", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_NamngerKalendernForPrenumeranten()
    {
        // Utan X-WR-CALNAME heter prenumerationen "Kalender" i telefonen, vilket är
        // obrukbart för en förälder med barn i två lag.
        var feed = await FeedForAsync(repository => repository.AddTeam("gul", "Gul"));

        Assert.Contains("X-WR-CALNAME:P2016 Gul", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_MatchFarStabiltUidOchRattTid()
    {
        Guid matchId = Guid.Empty;

        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            var match = repository.AddMatch(team, Kickoff);
            match.UpdatedUtc = Updated;
            matchId = match.Id;
        });

        Assert.Contains($"UID:{matchId}@karramatcher", feed, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20260830T111500Z", feed, StringComparison.Ordinal);
        Assert.Contains("DTSTAMP:20260825T060000Z", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_OforandradData_GerByteIdentiskFeed()
    {
        // Hela ETag:en bygger på det här. Sätts DTSTAMP från klockan i stället för från
        // datan blir feeden olika varje sekund: villkorade anrop får aldrig 304, och
        // kalenderappar laddar ner allt var sjätte timme fast ingenting ändrats.
        var (handler, repository) = Build();
        var team = repository.AddTeam("gul", "Gul");
        repository.AddMatch(team, Kickoff);

        var first = await handler.HandleAsync(new GetTeamCalendarQuery("gul"), CancellationToken.None);
        var second = await handler.HandleAsync(new GetTeamCalendarQuery("gul"), CancellationToken.None);

        Assert.Equal(first, second);
    }

    [Fact]
    public async Task HandleAsync_DtstampKommerFranDatanOchInteFranKlockan()
    {
        // Det här testet kan inte gå grönt av tur, vilket det förra gjorde lokalt: två
        // anrop inom samma sekund gav samma feed även när DTSTAMP kom från klockan.
        // Här jämförs värdet mot matchens UpdatedUtc, som ligger fem dagar bort.
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            var match = repository.AddMatch(team, Kickoff);
            match.UpdatedUtc = Updated;
        });

        Assert.Contains("DTSTAMP:20260825T060000Z", feed, StringComparison.Ordinal);
        Assert.DoesNotContain(
            $"DTSTAMP:{DateTime.UtcNow:yyyyMMdd}",
            feed,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_MatchUtanUppdateringsstampel_FallerTillbakaPaAvspark()
    {
        // Ska inte kunna hända -- databasen sätter alltid UpdatedUtc -- men ett DTSTAMP
        // som pekar på år 1 hade sett trasigt ut i en kalender.
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            repository.AddMatch(team, Kickoff);
        });

        Assert.Contains("DTSTAMP:20260830T111500Z", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_SequenceFoljerMatchen()
    {
        // §KM.4: utan ökande SEQUENCE ignorerar kalendern uppdateringen och föräldern
        // står kvar med den gamla tiden.
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            var match = repository.AddMatch(team, Kickoff);
            match.IcsSequence = 3;
        });

        Assert.Contains("SEQUENCE:3", feed, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled, "STATUS:CONFIRMED")]
    [InlineData(MatchStatus.Cancelled, "STATUS:CANCELLED")]
    [InlineData(MatchStatus.Postponed, "STATUS:TENTATIVE")]
    public async Task HandleAsync_StatusOversattsTillIcal(MatchStatus status, string expected)
    {
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            repository.AddMatch(team, Kickoff, status: status);
        });

        Assert.Contains(expected, feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_RubrikenInnehallerLagOchMotstandare()
    {
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            repository.AddMatch(team, Kickoff, opponent: "Torslanda IK");
        });

        Assert.Contains("SUMMARY:P2016 Gul (hemma) - Torslanda IK", feed, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_PlatsenEscapasSaAdressenInteDelas()
    {
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            repository.AddMatch(team, Kickoff);
        });

        Assert.Contains(
            "LOCATION:Karra IP\\, Idrottsvagen 1\\, Goteborg",
            feed,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_FeedenInnehallerEndastMatchdata()
    {
        // §KM.4 och Säkerhetschecklistan 5.7. Feeden är publik och oautentiserad, och en
        // prenumeration ligger kvar i en telefon i åratal. Fältuppsättningen låses därför:
        // ett nytt fält kräver ett beslut här, inte en upptäckt i någons kalender.
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            repository.AddMatch(team, Kickoff);
        });

        var properties = feed
            .Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith(' '))
            .Select(line => line.Split(':')[0].Split(';')[0])
            .Distinct()
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "BEGIN", "CALSCALE", "DTEND", "DTSTAMP", "DTSTART", "END", "LOCATION",
                "METHOD", "PRODID", "REFRESH-INTERVAL", "SEQUENCE", "STATUS", "SUMMARY",
                "UID", "VERSION", "X-PUBLISHED-TTL", "X-WR-CALNAME",
            ],
            properties);
    }

    [Fact]
    public async Task HandleAsync_MatcherKommerITidsordning()
    {
        var feed = await FeedForAsync(repository =>
        {
            var team = repository.AddTeam("gul", "Gul");
            repository.AddMatch(team, Kickoff.AddDays(7), "Sist");
            repository.AddMatch(team, Kickoff, "Forst");
        });

        Assert.True(
            feed.IndexOf("Forst", StringComparison.Ordinal)
                < feed.IndexOf("Sist", StringComparison.Ordinal),
            "Matcherna borde ligga i tidsordning i feeden");
    }
}
