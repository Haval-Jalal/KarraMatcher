using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams.GetTeamEvents;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Tests;

public class GetTeamEventsQueryHandlerTests
{
    private static readonly DateTime Kickoff =
        new(2026, 8, 29, 12, 30, 0, DateTimeKind.Utc);

    // Match-synligheten prövas i integrationstesterna (med riktiga roller/kallelser). Här räcker
    // en synlighet som visar allt (superadmin), så mappnings- och ordningstesterna står orörda.
    private static IMembershipService AllVisible() =>
        new StubMembershipService(new MatchVisibility(true, new HashSet<Guid>(), new HashSet<Guid>()));

    [Fact]
    public async Task HandleAsync_OkantLag_GerNull()
    {
        // Ett okänt lag är en felaktig länk, inte ett systemfel. Handlern säger "finns
        // inte" och controllern gör 404 av det — aldrig ett tomt schema, som hade sett
        // ut som en avslutad säsong.
        var repository = new FakeTeamRepository();
        var handler = new GetTeamEventsQueryHandler(repository, AllVisible());

        var result = await handler.HandleAsync(new GetTeamEventsQuery("finns-inte", Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_LagUtanMatcher_GerLagetMedTomLista()
    {
        var repository = new FakeTeamRepository();
        repository.AddTeam("gul", "Gul");
        var handler = new GetTeamEventsQueryHandler(repository, AllVisible());

        var result = await handler.HandleAsync(new GetTeamEventsQuery("gul", Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Gul", result.Team.Name);
        Assert.Empty(result.Events);
    }

    [Fact]
    public async Task HandleAsync_HamtarBaraDetEfterfragadeLagetsMatcher()
    {
        var repository = new FakeTeamRepository();
        var gul = repository.AddTeam("gul", "Gul");
        var bla = repository.AddTeam("bla", "Bla");
        repository.AddMatch(gul, Kickoff);
        repository.AddMatch(bla, Kickoff);
        var handler = new GetTeamEventsQueryHandler(repository, AllVisible());

        var result = await handler.HandleAsync(new GetTeamEventsQuery("gul", Guid.NewGuid()), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Single(result.Events);
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
        var handler = new GetTeamEventsQueryHandler(repository, AllVisible());

        var result = await handler.HandleAsync(new GetTeamEventsQuery("gul", Guid.NewGuid()), CancellationToken.None);

        var match = Assert.Single(result!.Events);
        Assert.Equal(TimeSpan.Zero, match.KickoffUtc.Offset);
        Assert.Equal(Kickoff, match.KickoffUtc.UtcDateTime);
    }

    [Fact]
    public async Task HandleAsync_InstalldMatch_ArMarkt()
    {
        var repository = new FakeTeamRepository();
        var team = repository.AddTeam("gul", "Gul");
        repository.AddMatch(team, Kickoff, status: EventStatus.Cancelled);
        var handler = new GetTeamEventsQueryHandler(repository, AllVisible());

        var result = await handler.HandleAsync(new GetTeamEventsQuery("gul", Guid.NewGuid()), CancellationToken.None);

        var match = Assert.Single(result!.Events);
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
        var handler = new GetTeamEventsQueryHandler(repository, AllVisible());

        var result = await handler.HandleAsync(new GetTeamEventsQuery("gul", Guid.NewGuid()), CancellationToken.None);

        var match = Assert.Single(result!.Events);
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
        var handler = new GetTeamEventsQueryHandler(repository, AllVisible());

        var result = await handler.HandleAsync(new GetTeamEventsQuery("gul", Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(
            ["Forst", "Mitten", "Sist"],
            result!.Events.Select(m => m.Opponent));
    }
}

/// <summary>
/// Testdubbel: bara <see cref="GetMatchVisibilityAsync"/> används av handlern. Resten kastar, så
/// ett oavsiktligt beroende syns direkt i stället för att tyst returnera fel.
/// </summary>
internal sealed class StubMembershipService(MatchVisibility visibility) : IMembershipService
{
    public Task<MatchVisibility> GetMatchVisibilityAsync(
        Guid accountId, CancellationToken cancellationToken) => Task.FromResult(visibility);

    public Task<bool> IsMemberOfTeamAsync(Guid accountId, Guid teamId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<MemberTruppDto>> MemberTrupperAsync(Guid accountId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<bool> IsLeaderOfTruppAsync(Guid accountId, Guid ageGroupId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<bool> IsMemberOfTruppAsync(Guid accountId, Guid ageGroupId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Guid>> MemberAccountIdsForTruppAsync(Guid ageGroupId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<Guid>> MemberAccountIdsAsync(Guid teamId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<bool> IsMemberOfTeamBySlugAsync(Guid accountId, string teamSlug, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<Guid?> TruppIdForTeamBySlugAsync(string teamSlug, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<bool> IsMemberOfEventAsync(Guid accountId, Guid eventId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<string?> TruppNameAsync(Guid ageGroupId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<TeamChannelInfo>> AccessibleTeamChannelsAsync(
        Guid accountId, Guid ageGroupId, CancellationToken ct) => throw new NotImplementedException();

    public Task<IReadOnlyList<string>> MemberTeamSlugsAsync(Guid accountId, CancellationToken ct) =>
        throw new NotImplementedException();
}
