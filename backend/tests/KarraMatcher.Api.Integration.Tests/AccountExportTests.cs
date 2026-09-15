using System.Net;
using System.Net.Http.Headers;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Push;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Registerutdrag (`#67`, checklistan 9.13).
///
/// <para>
/// Rätten till ett registerutdrag, som här blir liten just för att servern lagrar så lite.
/// Vaktar tre saker: att bara den inloggade får sitt eget, att utdraget faktiskt bär det
/// kontot äger, och att push-prenumerationens tekniska adress <em>aldrig</em> följer med
/// (§KM.10) — den är en secret, inte en uppgift att lämna ut.
/// </para>
/// </summary>
public sealed class AccountExportTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly DateTime Kickoff = new(2026, 10, 25, 11, 0, 0, DateTimeKind.Utc);

    private const string PushEndpoint = "https://fcm.example.com/hemlig-endpoint-abc123";
    private const string PushKey = "p256dh-hemlig-nyckel";

    private async Task<(Guid AccountId, SessionTokens Session, Guid TeamId)> SeedAccountWithDataAsync(
        string opponent)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var issuer = scope.ServiceProvider.GetRequiredService<SessionIssuer>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            FirstName = "Anna",
            CreatedUtc = Kickoff.AddYears(-1),
        };

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"club-{Guid.NewGuid():N}" };
        var ageGroup = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "P2016",
            Season = "2026",
        };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"team-{Guid.NewGuid():N}",
        };
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Karra IP",
            Address = "Idrottsvagen 1, Goteborg",
            Latitude = 57.79,
            Longitude = 11.94,
            IsHome = true,
        };
        var match = new Match
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = Kickoff,
            OpponentName = opponent,
            VenueId = venue.Id,
            IsHome = true,
            Status = MatchStatus.Scheduled,
            UpdatedUtc = Kickoff,
        };
        var offer = new CarpoolOffer
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            DriverAccountId = account.Id,
            Direction = CarpoolDirection.Both,
            DeparturePlace = "Kärra centrum",
            DepartureUtc = Kickoff.AddHours(-1),
            Seats = 3,
            Note = "Har plats för en till",
            Status = CarpoolOfferStatus.Open,
            CreatedUtc = Kickoff.AddDays(-2),
            UpdatedUtc = Kickoff.AddDays(-2),
        };
        var subscription = new PushSubscription
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            AccountId = account.Id,
            Endpoint = PushEndpoint,
            P256dh = PushKey,
            Auth = "auth-hemlig",
            CreatedUtc = Kickoff.AddDays(-3),
        };
        var preference = new NotificationPreference
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            TeamId = team.Id,
            MatchChanges = true,
            Carpool = false,
            Reminders = true,
            UpdatedUtc = Kickoff.AddDays(-3),
        };

        context.Accounts.Add(account);
        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.Add(match);
        context.CarpoolOffers.Add(offer);
        context.PushSubscriptions.Add(subscription);
        context.NotificationPreferences.Add(preference);

        await context.SaveChangesAsync(CancellationToken.None);

        var session = await issuer.IssueAsync(account, CancellationToken.None);

        return (account.Id, session, team.Id);
    }

    private async Task<(HttpStatusCode Status, string Body)> ExportAsync(string? accessToken)
    {
        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/export");

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await client.SendAsync(request, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        return (response.StatusCode, body);
    }

    [Fact]
    public async Task Export_UtanInloggning_Nekas()
    {
        var (status, _) = await ExportAsync(accessToken: null);

        Assert.Equal(HttpStatusCode.Unauthorized, status);
    }

    [Fact]
    public async Task Export_GerKontotsEgenData()
    {
        var (_, session, _) = await SeedAccountWithDataAsync("Torslanda IK");

        var (status, body) = await ExportAsync(session.AccessToken);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Contains("Torslanda IK", body, StringComparison.Ordinal);
        Assert.Contains("Kärra centrum", body, StringComparison.Ordinal);
        Assert.Contains("Gul", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_NamnerSpelarkortetSomFranvarande()
    {
        // §KM.2: kortet finns inte här och det ska stå rakt ut, med hänvisning till koden.
        var (_, session, _) = await SeedAccountWithDataAsync("Torslanda IK");

        var (_, body) = await ExportAsync(session.AccessToken);

        Assert.Contains("säkerhetskopieringskoden", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Export_LamnarAldrigUtPushSecreten()
    {
        // §KM.10: push-endpoint och nycklar är secrets. De får inte finnas i utdraget, ens
        // för den som äger prenumerationen — projektionen väljer aldrig ut dem.
        var (_, session, _) = await SeedAccountWithDataAsync("Torslanda IK");

        var (_, body) = await ExportAsync(session.AccessToken);

        Assert.DoesNotContain(PushEndpoint, body, StringComparison.Ordinal);
        Assert.DoesNotContain(PushKey, body, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-hemlig", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_TarInteMedAnnatKontosData()
    {
        // Objektnivå-auktorisering: id:t kommer ur token, och en förälder ser bara sitt eget.
        var mine = await SeedAccountWithDataAsync("Mitt Lag");
        await SeedAccountWithDataAsync("Annans Motstandare");

        var (_, body) = await ExportAsync(mine.Session.AccessToken);

        Assert.Contains("Mitt Lag", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Annans Motstandare", body, StringComparison.Ordinal);
    }
}
