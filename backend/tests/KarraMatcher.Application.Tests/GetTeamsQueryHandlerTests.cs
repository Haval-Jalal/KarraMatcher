using KarraMatcher.Application.Features.Teams.GetTeams;

namespace KarraMatcher.Application.Tests;

public class GetTeamsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_IngaLag_GerTomLista()
    {
        var handler = new GetTeamsQueryHandler(new FakeTeamRepository());

        var result = await handler.HandleAsync(new GetTeamsQuery(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task HandleAsync_MapparFaltenAppenVisar()
    {
        var repository = new FakeTeamRepository();
        repository.AddTeam("gul", "Gul", "#D9A21B", FakeTeamRepository.NewAgeGroup("P2016"));
        var handler = new GetTeamsQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamsQuery(), CancellationToken.None);

        var team = Assert.Single(result);
        Assert.Equal("gul", team.Slug);
        Assert.Equal("Gul", team.Name);
        Assert.Equal("P2016", team.AgeGroup);
        Assert.Equal("#D9A21B", team.ColorHex);
    }

    [Fact]
    public async Task HandleAsync_LagUtanAldersgrupp_GerTomStrangIStalletForKrasch()
    {
        // AgeGroup ar en navigering och kan vara oinlast. Ett schema ska aldrig falla
        // pa att en referens inte hangde med i fragan.
        var repository = new FakeTeamRepository();
        var team = repository.AddTeam("gul", "Gul");
        team.AgeGroup = null;
        var handler = new GetTeamsQueryHandler(repository);

        var result = await handler.HandleAsync(new GetTeamsQuery(), CancellationToken.None);

        Assert.Equal(string.Empty, Assert.Single(result).AgeGroup);
    }
}
