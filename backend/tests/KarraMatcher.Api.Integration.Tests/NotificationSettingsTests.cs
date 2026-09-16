using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Push;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Notisinställningar per användare och lag (`#65`).
///
/// <para>
/// Två sidor prövas: att en förälder kan läsa och sätta sina val (allt på som förval), och
/// att valen faktiskt respekteras vid utskick — den som stängt av en kategori lämnas ute,
/// medan en anonym prenumerant utan konto får allt som förr.
/// </para>
/// </summary>
public sealed class NotificationSettingsTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(string Slug, Guid TeamId, Guid AccountId);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-n-{suffix}" };
        var ageGroup = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-n-{suffix}",
        };
        var account = new Account { Id = Guid.NewGuid(), Email = $"foralder-n-{suffix}@example.com", CreatedUtc = DateTime.UtcNow };

        // Kontot ar medlem av laget (v2, §KM.3): vardnadshavare till ett barn i det. Utan
        // medlemskap kommer man inte at lagets notisinstallningar.
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = ageGroup.Id,
            TeamId = team.Id,
            CreatedUtc = DateTime.UtcNow,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Accounts.Add(account);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            ChildId = child.Id,
            GrantedUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Slug, team.Id, account.Id);
    }

    private string TokenFor(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [], [])).Token;
    }

    private async Task<Guid> AddSubscriberAsync(Guid teamId, Guid? accountId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var subscription = new PushSubscription
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            AccountId = accountId,
            Endpoint = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}",
            P256dh = "BLc4xRzKlKORKWlbdgFaBrrPK3ydWAHo4M0gs0i1oEKgPpWG5nnwyPCwbLwGvHqvqnfHiPSw1kvR8t9zs2VoXsc",
            Auth = "8eDyX_uCN0XRhSbY5hs7Hg",
            CreatedUtc = DateTime.UtcNow,
        };

        context.PushSubscriptions.Add(subscription);
        await context.SaveChangesAsync(CancellationToken.None);

        return subscription.Id;
    }

    private async Task DisableAsync(
        Guid accountId,
        Guid teamId,
        bool matchChanges = true,
        bool carpool = true,
        bool reminders = true)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        context.NotificationPreferences.Add(new NotificationPreference
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            TeamId = teamId,
            MatchChanges = matchChanges,
            Carpool = carpool,
            Reminders = reminders,
            UpdatedUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(CancellationToken.None);
    }

    private async Task<IReadOnlyList<Guid>> DeliverToTeamAsync(Guid teamId, PushCategory category)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPushDeliveryRepository>();

        var targets = await repository.ListForTeamAsync(teamId, category, CancellationToken.None);
        return [.. targets.Select(t => t.Id)];
    }

    private async Task<IReadOnlyList<Guid>> DeliverToAccountAsync(
        Guid teamId,
        Guid accountId,
        PushCategory category)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPushDeliveryRepository>();

        var targets = await repository.ListForAccountsAsync(teamId, [accountId], category, CancellationToken.None);
        return [.. targets.Select(t => t.Id)];
    }

    private async Task<HttpResponseMessage> GetAsync(string slug, string token)
    {
        using var client = factory.CreateClient(ClientOptions);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/teams/{slug}/notification-settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task<HttpResponseMessage> PutAsync(string slug, string token, object body)
    {
        using var client = factory.CreateClient(ClientOptions);

        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        csrfResponse.EnsureSuccessStatusCode();

        var csrfBody = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/teams/{slug}/notification-settings")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrfBody.GetProperty("token").GetString()!);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Inställningarna själva -------------------------------------------------------

    [Fact]
    public async Task Standard_ArAlltPa()
    {
        var fixture = await SeedAsync("default");

        var response = await GetAsync(fixture.Slug, TokenFor(fixture.AccountId));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.True(body.GetProperty("matchChanges").GetBoolean());
        Assert.True(body.GetProperty("carpool").GetBoolean());
        Assert.True(body.GetProperty("reminders").GetBoolean());
    }

    [Fact]
    public async Task SpararOchLaserTillbaka()
    {
        var fixture = await SeedAsync("save");
        var token = TokenFor(fixture.AccountId);

        var put = await PutAsync(fixture.Slug, token, new { matchChanges = true, carpool = false, reminders = false });
        put.EnsureSuccessStatusCode();

        var get = await GetAsync(fixture.Slug, token);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.True(body.GetProperty("matchChanges").GetBoolean());
        Assert.False(body.GetProperty("carpool").GetBoolean());
        Assert.False(body.GetProperty("reminders").GetBoolean());
    }

    [Fact]
    public async Task OkantLag_Nekas()
    {
        // Stangd app (§KM.3, #191): ett okant lag har inga medlemmar, sa medlemskapskravet
        // nekar med 403 innan controllern hinner svara 404 -- existensen avslojas inte.
        var fixture = await SeedAsync("unknown");

        var response = await GetAsync("finns-inte", TokenFor(fixture.AccountId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UtanInloggning_Nekas()
    {
        var fixture = await SeedAsync("anon");

        using var client = factory.CreateClient();
        var response = await client.GetAsync(
            $"/api/v1/teams/{fixture.Slug}/notification-settings", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Respekteras vid utskick -----------------------------------------------------

    [Fact]
    public async Task LagbrettUtskick_UteslutDenSomStangtAv()
    {
        var fixture = await SeedAsync("team-filter");
        var mine = await AddSubscriberAsync(fixture.TeamId, fixture.AccountId);
        var anonymous = await AddSubscriberAsync(fixture.TeamId, accountId: null);
        await DisableAsync(fixture.AccountId, fixture.TeamId, matchChanges: false);

        var reached = await DeliverToTeamAsync(fixture.TeamId, PushCategory.MatchChange);

        Assert.DoesNotContain(mine, reached);
        Assert.Contains(anonymous, reached); // en gäst har inga inställningar och får allt
    }

    [Fact]
    public async Task LagbrettUtskick_AnnanKategoriNasFortfarande()
    {
        // Bara matchändringar avstängt -- påminnelser når fortfarande fram.
        var fixture = await SeedAsync("category");
        var mine = await AddSubscriberAsync(fixture.TeamId, fixture.AccountId);
        await DisableAsync(fixture.AccountId, fixture.TeamId, matchChanges: false);

        var reached = await DeliverToTeamAsync(fixture.TeamId, PushCategory.Reminder);

        Assert.Contains(mine, reached);
    }

    [Fact]
    public async Task KontoriktatUtskick_UteslutDenSomStangtAvSamakning()
    {
        var fixture = await SeedAsync("account-filter");
        var mine = await AddSubscriberAsync(fixture.TeamId, fixture.AccountId);
        await DisableAsync(fixture.AccountId, fixture.TeamId, carpool: false);

        var carpool = await DeliverToAccountAsync(fixture.TeamId, fixture.AccountId, PushCategory.Carpool);
        var matchChange = await DeliverToAccountAsync(fixture.TeamId, fixture.AccountId, PushCategory.MatchChange);

        Assert.DoesNotContain(mine, carpool);
        Assert.Contains(mine, matchChange); // bara samåkning avstängt
    }
}
