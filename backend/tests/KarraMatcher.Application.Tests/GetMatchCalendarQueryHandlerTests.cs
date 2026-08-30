using KarraMatcher.Application.Features.Calendar.GetMatchCalendar;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Application.Tests;

public class GetMatchCalendarQueryHandlerTests
{
    private static Match NewMatch(DateTime kickoffUtc, string slug = "gul")
    {
        var ageGroup = FakeTeamRepository.NewAgeGroup();

        return new Match
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
                Slug = slug,
            },
            KickoffUtc = kickoffUtc,
            OpponentName = "Torslanda IK",
            VenueId = Guid.NewGuid(),
            Venue = FakeTeamRepository.NewVenue(),
            IsHome = true,
            Status = MatchStatus.Scheduled,
        };
    }

    private static (GetMatchCalendarQueryHandler Handler, FakeMatchRepository Repository) Build()
    {
        var repository = new FakeMatchRepository();

        return (new GetMatchCalendarQueryHandler(repository), repository);
    }

    [Fact]
    public async Task HandleAsync_OkantId_GerNull()
    {
        var (handler, _) = Build();

        var result = await handler.HandleAsync(
            new GetMatchCalendarQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_GerEnKalenderMedEnEndaHandelse()
    {
        var (handler, repository) = Build();
        var match = NewMatch(new DateTime(2026, 8, 30, 11, 15, 0, DateTimeKind.Utc));
        repository.Matches.Add(match);

        var result = await handler.HandleAsync(
            new GetMatchCalendarQuery(match.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(
            result.Content.Split("BEGIN:VEVENT", StringSplitOptions.None).Skip(1));
        Assert.Contains("SUMMARY:P2016 Gul (hemma) - Torslanda IK", result.Content, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20260830T111500Z", result.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_FilnamnetInnehallerLagOchDatum()
    {
        var (handler, repository) = Build();
        var match = NewMatch(new DateTime(2026, 8, 30, 11, 15, 0, DateTimeKind.Utc));
        repository.Matches.Add(match);

        var result = await handler.HandleAsync(
            new GetMatchCalendarQuery(match.Id), CancellationToken.None);

        Assert.Equal("karra-gul-2026-08-30.ics", result!.FileName);
    }

    [Fact]
    public async Task HandleAsync_FilnamnetAnvanderSvenskDag_InteUtcDag()
    {
        // 30 augusti 22:30 UTC är 31 augusti 00:30 i Sverige. En fil som heter fel dag är
        // svår att hitta igen bland nedladdningarna (§KM.5).
        var (handler, repository) = Build();
        var match = NewMatch(new DateTime(2026, 8, 30, 22, 30, 0, DateTimeKind.Utc));
        repository.Matches.Add(match);

        var result = await handler.HandleAsync(
            new GetMatchCalendarQuery(match.Id), CancellationToken.None);

        Assert.Equal("karra-gul-2026-08-31.ics", result!.FileName);
    }

    [Fact]
    public async Task HandleAsync_MatchUtanLag_GerNull()
    {
        var (handler, repository) = Build();
        var match = NewMatch(new DateTime(2026, 8, 30, 11, 15, 0, DateTimeKind.Utc));
        match.Team = null;
        repository.Matches.Add(match);

        var result = await handler.HandleAsync(
            new GetMatchCalendarQuery(match.Id), CancellationToken.None);

        Assert.Null(result);
    }
}
