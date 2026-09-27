using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Adminens cup-lags-bygge (`#335`, slice 4 av #330). Admin bygger tillfälliga cup-lag av de barn
/// som anmält sig till cupen. Vaktar: bara anmälda barn kan placeras (oanmält → 409), placeringen
/// syns i cupens sammanställning, och bara en admin (inte en vårdnadshavare) kan bygga lagen.
/// </summary>
public sealed class CupTeamTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(
        Guid TruppId, Guid CupId, Guid SignedUpChild, Guid NotSignedUpChild, Guid Guardian);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-ct-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-ct-{suffix}",
        };
        var cup = new Event
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = team.Id,
            Type = EventType.Cup,
            KickoffUtc = now.AddDays(7),
            Title = "Sommarcup",
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };

        var guardian = new Account { Id = Guid.NewGuid(), Email = $"vh-ct-{suffix}@example.com", CreatedUtc = now };
        var signedUp = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = trupp.Id,
            TeamId = team.Id,
            CreatedUtc = now,
        };
        var notSignedUp = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Noah",
            LastInitial = "K",
            AgeGroupId = trupp.Id,
            TeamId = team.Id,
            CreatedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Events.Add(cup);
        context.Accounts.Add(guardian);
        context.Children.AddRange(signedUp, notSignedUp);
        context.Guardianships.AddRange(
            new Guardianship { Id = Guid.NewGuid(), AccountId = guardian.Id, ChildId = signedUp.Id, GrantedUtc = now },
            new Guardianship { Id = Guid.NewGuid(), AccountId = guardian.Id, ChildId = notSignedUp.Id, GrantedUtc = now });

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(trupp.Id, cup.Id, signedUp.Id, notSignedUp.Id, guardian.Id);
    }

    private string AdminToken(Guid truppId) =>
        Token(Guid.NewGuid(), new AccountRoles(false, [truppId.ToString()], []));

    private string GuardianToken(Guid guardianId) => Token(guardianId, AccountRoles.None);

    private string Token(Guid accountId, AccountRoles roles)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer.Issue(accountId, "konto@example.com", roles).Token;
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string token, object? payload = null) =>
        SendViaAsync(factory, method, path, token, payload);

    private static async Task<HttpResponseMessage> SendViaAsync(
        WebApplicationFactory<Program> app,
        HttpMethod method,
        string path,
        string token,
        object? payload = null)
    {
        using var client = app.CreateClient(ClientOptions);
        var (csrf, cookie) = await CsrfAsync(client, token);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        using var client = factory.CreateClient(ClientOptions);
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
    }

    private static async Task<(string Token, string Cookie)> CsrfAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        return (body.GetProperty("token").GetString()!, cookie);
    }

    private async Task OpenAndSignUpAsync(Fixture f)
    {
        (await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.CupId}/cup",
            AdminToken(f.TruppId),
            new { capacity = 10 })).EnsureSuccessStatusCode();

        (await SendAsync(
            HttpMethod.Post,
            $"/api/v1/events/{f.CupId}/cup/children/{f.SignedUpChild}",
            GuardianToken(f.Guardian))).EnsureSuccessStatusCode();
    }

    private async Task<Guid> CreateTeamAsync(Fixture f, string name)
    {
        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.CupId}/cup/teams",
            AdminToken(f.TruppId),
            new { name });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return body.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Admin_ByggerCupLag_OchPlacerarAnmaltBarn()
    {
        var f = await SeedAsync("build");
        await OpenAndSignUpAsync(f);

        var teamId = await CreateTeamAsync(f, "Lag 1");

        var assign = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.CupId}/cup/teams/{teamId}/children/{f.SignedUpChild}",
            AdminToken(f.TruppId));
        Assert.Equal(HttpStatusCode.NoContent, assign.StatusCode);

        // Placeringen syns i cupens sammanställning.
        var summary = await GetAsync($"/api/v1/events/{f.CupId}/cup", GuardianToken(f.Guardian));
        var body = await summary.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        var team = Assert.Single(body.GetProperty("teams").EnumerateArray());
        Assert.Equal("Lag 1", team.GetProperty("name").GetString());
        var member = Assert.Single(team.GetProperty("members").EnumerateArray());
        Assert.Equal(f.SignedUpChild, member.GetProperty("childId").GetGuid());
        Assert.Equal("Liam J", member.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task Admin_PlacerarOanmaltBarn_Nekas()
    {
        var f = await SeedAsync("unsigned");
        await OpenAndSignUpAsync(f);
        var teamId = await CreateTeamAsync(f, "Lag 1");

        var assign = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.CupId}/cup/teams/{teamId}/children/{f.NotSignedUpChild}",
            AdminToken(f.TruppId));

        Assert.Equal(HttpStatusCode.Conflict, assign.StatusCode);
    }

    [Fact]
    public async Task Guardian_KanInteByggaCupLag()
    {
        var f = await SeedAsync("forbidden");
        await OpenAndSignUpAsync(f);

        var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.CupId}/cup/teams",
            GuardianToken(f.Guardian),
            new { name = "Lag 1" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnmalanOppnas_NotisTillHelaTruppen()
    {
        // När admin öppnar anmälan får hela truppen en notis (`#335`) — riktad mot truppen, inte
        // ett lag, och utan barn-PII (§KM.1/§KM.10).
        var f = await SeedAsync("notice");
        var outbox = new RecordingOutbox();

        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushOutbox>();
            services.AddSingleton<IPushOutbox>(outbox);
        }));

        (await SendViaAsync(
            app,
            HttpMethod.Put,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.CupId}/cup",
            AdminToken(f.TruppId),
            new { capacity = 10 })).EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal(f.TruppId, dispatch.AgeGroupId); // ToTrupp — hela truppen
        Assert.Null(dispatch.TeamId);
        Assert.Contains("Anmälan öppen", dispatch.Message.Title, StringComparison.Ordinal);
    }
}
