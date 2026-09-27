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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kontots globala notisinställning — en enda på/av (`#332`-uppföljning, ersätter per-typ `#65`).
///
/// <para>
/// Två sidor prövas: att en förälder kan läsa och sätta sin på/av (på som förval), och att
/// valet respekteras vid utskick — den som stängt av notiser får ingen push, medan
/// medlemskaps- och kontoriktnings-reglerna (`#200`) är oförändrade.
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

    /// <summary>Stänger av notiser globalt för kontot.</summary>
    private async Task DisableNotificationsAsync(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = await context.Accounts.SingleAsync(a => a.Id == accountId, CancellationToken.None);
        account.NotificationsEnabled = false;
        await context.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>Ett extra konto som är medlem av laget (tränare), så en lag-notis når det.</summary>
    private async Task<Guid> AddCoachAsync(Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"tranare-{Guid.NewGuid():N}@example.com",
            CreatedUtc = DateTime.UtcNow,
        };
        context.Accounts.Add(account);
        context.TeamRoles.Add(new TeamRole
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            TeamId = teamId,
            Role = RoleKind.Coach,
            GrantedUtc = DateTime.UtcNow,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        return account.Id;
    }

    private async Task<IReadOnlyList<Guid>> DeliverToTeamAsync(Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPushDeliveryRepository>();

        var targets = await repository.ListForTeamAsync(teamId, PushCategory.EventChange, CancellationToken.None);
        return [.. targets.Select(t => t.Id)];
    }

    private async Task<IReadOnlyList<Guid>> DeliverToAccountAsync(Guid teamId, Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IPushDeliveryRepository>();

        var targets = await repository.ListForAccountsAsync(
            teamId, [accountId], PushCategory.Carpool, CancellationToken.None);
        return [.. targets.Select(t => t.Id)];
    }

    private async Task<HttpResponseMessage> GetSettingsAsync(string token)
    {
        using var client = factory.CreateClient(ClientOptions);
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notification-settings");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task<HttpResponseMessage> PutSettingsAsync(string token, object body)
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

        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/notification-settings")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrfBody.GetProperty("token").GetString()!);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Inställningen själv ----------------------------------------------------------

    [Fact]
    public async Task Standard_ArPa()
    {
        var fixture = await SeedAsync("default");

        var response = await GetSettingsAsync(TokenFor(fixture.AccountId));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.True(body.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task SpararOchLaserTillbaka()
    {
        var fixture = await SeedAsync("save");
        var token = TokenFor(fixture.AccountId);

        var put = await PutSettingsAsync(token, new { enabled = false });
        put.EnsureSuccessStatusCode();

        var get = await GetSettingsAsync(token);
        var body = await get.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.False(body.GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public async Task UtanInloggning_Nekas()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/notification-settings", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Respekteras vid utskick -----------------------------------------------------

    [Fact]
    public async Task LagbrettUtskick_NarBaraMedlemmar()
    {
        // #200: en lag-notis går bara till medlemmar. En prenumeration som hör till ett konto
        // som inte är medlem, och en anonym prenumeration (utan konto), nås inte.
        var fixture = await SeedAsync("member-only");
        var member = await AddSubscriberAsync(fixture.TeamId, fixture.AccountId);

        var nonMemberAccount = await SeedOutsiderAsync("member-only");
        var nonMember = await AddSubscriberAsync(fixture.TeamId, nonMemberAccount);
        var anonymous = await AddSubscriberAsync(fixture.TeamId, accountId: null);

        var reached = await DeliverToTeamAsync(fixture.TeamId);

        Assert.Contains(member, reached);
        Assert.DoesNotContain(nonMember, reached);
        Assert.DoesNotContain(anonymous, reached);
    }

    [Fact]
    public async Task LagbrettUtskick_UteslutDenSomStangtAvNotiser()
    {
        var fixture = await SeedAsync("team-filter");
        var mine = await AddSubscriberAsync(fixture.TeamId, fixture.AccountId);

        // En annan medlem med notiser på ska fortfarande nås — positiv kontroll.
        var otherAccount = await AddCoachAsync(fixture.TeamId);
        var other = await AddSubscriberAsync(fixture.TeamId, otherAccount);

        await DisableNotificationsAsync(fixture.AccountId);

        var reached = await DeliverToTeamAsync(fixture.TeamId);

        Assert.DoesNotContain(mine, reached);
        Assert.Contains(other, reached);
    }

    [Fact]
    public async Task KontoriktatUtskick_UteslutDenSomStangtAvNotiser()
    {
        var fixture = await SeedAsync("account-filter");
        var mine = await AddSubscriberAsync(fixture.TeamId, fixture.AccountId);
        await DisableNotificationsAsync(fixture.AccountId);

        var reached = await DeliverToAccountAsync(fixture.TeamId, fixture.AccountId);

        Assert.DoesNotContain(mine, reached);
    }

    [Fact]
    public async Task KontoriktatUtskick_NarAvenIckeMedlem()
    {
        // #200: ett kontoriktat utskick är inte medlemskaps-grindat — så här når en notis ett
        // inlånat barns vårdnadshavare som inte är medlem av händelsens lag.
        var fixture = await SeedAsync("account-nonmember");
        var outsider = await SeedOutsiderAsync("account-nonmember");
        var subscription = await AddSubscriberAsync(fixture.TeamId, outsider);

        var reached = await DeliverToAccountAsync(fixture.TeamId, outsider);

        Assert.Contains(subscription, reached);
    }

    /// <summary>Ett konto utan någon koppling till laget — för att pröva medlemskaps-grinden.</summary>
    private async Task<Guid> SeedOutsiderAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"utomstaende-{suffix}-{Guid.NewGuid():N}@example.com",
            CreatedUtc = DateTime.UtcNow,
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync(CancellationToken.None);

        return account.Id;
    }
}
