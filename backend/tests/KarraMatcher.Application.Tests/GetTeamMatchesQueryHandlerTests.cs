using KarraMatcher.Application.Features.Teams.GetTeamMatches;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Tests;

public class GetTeamMatchesQueryHandlerTests
{
    private static readonly DateTime Kickoff =
        new(2026, 8, 29, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_OkantLag_GerNull()
    {
        // Ett okänt lag är en felaktig länk, inte ett systemfel. Handlern säger "finns
        // inte" och controllern gör 404 av det — aldrig ett tomt schema, som hade sett
        // ut som en avslutad säsong.
        var repository = new FakeTeamRepository();
        var handler = new GetTeamMatchesQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamMatchesQuery("finns-inte"), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_LagUtanMatcher_GerLagetMedTomLista()
    {
        var repository = new FakeTeamRepository();
        repository.AddTeam("gul", "Gul");
        var handler = new GetTeamMatchesQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamMatchesQuery("gul"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Gul", result.Team.Name);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task HandleAsync_HamtarBaraDetEfterfragadeLagetsMatcher()
    {
        var repository = new FakeTeamRepository();
        var gul = repository.AddTeam("gul", "Gul");
        var bla = repository.AddTeam("bla", "Bla");
        repository.AddMatch(gul, Kickoff);
        repository.AddMatch(bla, Kickoff);
        var handler = new GetTeamMatchesQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamMatchesQuery("gul"), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Matches);
        Assert.Equal(gul.Id, repository.LastRequestedTeamId);
    }

    [Fact]
    public async Task HandleAsync_AvsparkLamnarServernIUtc()
    {
        // §KM.5: lagring och överföring i UTC, konvertering till Europe/Stockholm sker på
        // ett enda ställe i frontenden. Skulle backend börja skicka lokaltid vore felet
        // osynligt halva året och en timme fel den andra halvan.
        var repository = new FakeTeamRepository();
        var team = repository.AddTeam("gul", "Gul");
        repository.AddMatch(team, Kickoff);
        var handler = new GetTeamMatchesQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamMatchesQuery("gul"), CancellationToken.None);

        var match = Assert.Single(result!.Matches);
        Assert.Equal(TimeSpan.Zero, match.KickoffUtc.Offset);
        Assert.Equal(Kickoff, match.KickoffUtc.UtcDateTime);
    }

    [Fact]
    public async Task HandleAsync_InstalldMatch_ArMarkt()
    {
        var repository = new FakeTeamRepository();
        var team = repository.AddTeam("gul", "Gul");
        repository.AddMatch(team, Kickoff, status: MatchStatus.Cancelled);
        var handler = new GetTeamMatchesQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamMatchesQuery("gul"), CancellationToken.None);

        var match = Assert.Single(result!.Matches);
        Assert.Equal("Cancelled", match.Status);
    }

    [Theory]
    [InlineData(null, "Idrottsvagen 1, Goteborg")]
    [InlineData("", "Idrottsvagen 1, Goteborg")]
    [InlineData("   ", "Idrottsvagen 1, Goteborg")]
    [InlineData("Bortavagen 9, Molndal", "Bortavagen 9, Molndal")]
    public async Task HandleAsync_AvvikandeAdress_VinnerOverSpelplatsens(
        string? addressOverride,
        string expected)
    {
        // Tom sträng räknas som "ingen avvikelse". Annars hade en tränare som tömt
        // textrutan råkat radera adressen för hela matchen.
        var repository = new FakeTeamRepository();
        var team = repository.AddTeam("gul", "Gul");
        repository.AddMatch(team, Kickoff, addressOverride: addressOverride);
        var handler = new GetTeamMatchesQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamMatchesQuery("gul"), CancellationToken.None);

        var match = Assert.Single(result!.Matches);
        Assert.Equal(expected, match.Address);
        Assert.Equal("Idrottsvagen 1, Goteborg", match.Venue.Address);
    }

    [Fact]
    public async Task HandleAsync_MatcherKommerITidsordning()
    {
        var repository = new FakeTeamRepository();
        var team = repository.AddTeam("gul", "Gul");
        repository.AddMatch(team, Kickoff.AddDays(7), "Sist");
        repository.AddMatch(team, Kickoff, "Forst");
        repository.AddMatch(team, Kickoff.AddDays(3), "Mitten");
        var handler = new GetTeamMatchesQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamMatchesQuery("gul"), CancellationToken.None);

        Assert.Equal(
            ["Forst", "Mitten", "Sist"],
            result!.Matches.Select(m => m.Opponent));
    }
}
