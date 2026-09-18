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
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Push;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Närvarosummeringens "inte svarat" och påminnelsen (`#58`, §KM.1, §KM.12).
///
/// <para>
/// "Inte svarat" mäts mot lagets prenumeranter med konto — den enda konto-baserade lag-
/// kopplingen (den kom i <c>#63</c>), och den som kan ta emot en påminnelse. Påminnelsen går
/// bara till dem, aldrig till någon som redan svarat, och bär ingen fritext.
/// </para>
/// </summary>
public sealed class AttendanceReminderTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private sealed record Fixture(string Slug, Guid TeamId, Guid MatchId, Guid CoachId);

    private (WebApplicationFactory<Program> App, RecordingOutbox Outbox) WithRecordingOutbox()
    {
        var outbox = new RecordingOutbox();
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushOutbox>();
            services.AddSingleton<IPushOutbox>(outbox);
        }));

        return (app, outbox);
    }

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-r-{suffix}" };
        var ageGroup = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-r-{suffix}",
            AttendanceEnabled = true,
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
        var match = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = now.AddDays(3),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = now,
        };
        var coach = new Account { Id = Guid.NewGuid(), Email = $"tranare-r-{suffix}@example.com", CreatedUtc = now };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.Add(match);
        context.Accounts.Add(coach);
        context.AttendanceCalls.Add(new AttendanceCall
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            OpenedByAccountId = coach.Id,
            OpenedUtc = now,
        });

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Slug, team.Id, match.Id, coach.Id);
    }

    /// <summary>Ett konto som prenumererar på laget, valfritt med ett svar på matchen.</summary>
    private async Task<Guid> SeedSubscriberAsync(
        Fixture fixture,
        string name,
        AttendanceStatus? answered = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{name.ToLowerInvariant()}-{Guid.NewGuid():N}@example.com",
            FirstName = name,
            CreatedUtc = now,
        };
        context.Accounts.Add(account);
        context.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(),
            TeamId = fixture.TeamId,
            AccountId = account.Id,
            Endpoint = $"https://fcm.googleapis.com/fcm/send/{Guid.NewGuid():N}",
            P256dh = "BLc4xRzKlKORKWlbdgFaBrrPK3ydWAHo4M0gs0i1oEKgPpWG5nnwyPCwbLwGvHqvqnfHiPSw1kvR8t9zs2VoXsc",
            Auth = "8eDyX_uCN0XRhSbY5hs7Hg",
            CreatedUtc = now,
        });

        if (answered is not null)
        {
            context.AttendanceResponses.Add(new AttendanceResponse
            {
                Id = Guid.NewGuid(),
                MatchId = fixture.MatchId,
                AccountId = account.Id,
                Status = answered.Value,
                Count = answered.Value == AttendanceStatus.CantCome ? 0 : 2,
                CreatedUtc = now,
                UpdatedUtc = now,
            });
        }

        await context.SaveChangesAsync(CancellationToken.None);

        return account.Id;
    }

    private string TokenFor(Guid accountId, params string[] coachOf)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [], coachOf)).Token;
    }

    private static async Task<HttpResponseMessage> GetAsync(
        WebApplicationFactory<Program> app,
        string path,
        string token)
    {
        using var client = app.CreateClient(ClientOptions);
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
    }

    private static async Task<HttpResponseMessage> PostAsync(
        WebApplicationFactory<Program> app,
        string path,
        string token)
    {
        using var client = app.CreateClient(ClientOptions);

        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        csrfResponse.EnsureSuccessStatusCode();

        var body = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", body.GetProperty("token").GetString()!);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    private static string Base(Fixture f) => $"/api/v1/teams/{f.Slug}/matches/{f.MatchId}/attendance";

    [Fact]
    public async Task Summering_RaknarDemSomInteSvarat()
    {
        var fixture = await SeedAsync("count");
        await SeedSubscriberAsync(fixture, "Anna", AttendanceStatus.Coming);
        await SeedSubscriberAsync(fixture, "Bengt");
        var (app, _) = WithRecordingOutbox();

        var response = await GetAsync(app, $"{Base(fixture)}/summary", TokenFor(fixture.CoachId, fixture.Slug));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(1, body.GetProperty("notAnsweredCount").GetInt32());
        var names = body.GetProperty("notAnsweredNames").EnumerateArray().Select(n => n.GetString()).ToArray();
        Assert.Contains("Bengt", names);
        Assert.DoesNotContain("Anna", names);
    }

    [Fact]
    public async Task Paminnelse_NarBaraDemSomInteSvarat()
    {
        var fixture = await SeedAsync("remind");
        await SeedSubscriberAsync(fixture, "Anna", AttendanceStatus.Coming);
        var bengt = await SeedSubscriberAsync(fixture, "Bengt");
        var (app, outbox) = WithRecordingOutbox();

        var response = await PostAsync(app, $"{Base(fixture)}/remind", TokenFor(fixture.CoachId, fixture.Slug));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(1, body.GetProperty("reminded").GetInt32());

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal([bengt], dispatch.AccountIds);
        Assert.Contains("Påminnelse", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Paminnelse_UtanNagonAttPaminna_KoarIngenNotis()
    {
        var fixture = await SeedAsync("none");
        await SeedSubscriberAsync(fixture, "Anna", AttendanceStatus.Coming);
        var (app, outbox) = WithRecordingOutbox();

        var response = await PostAsync(app, $"{Base(fixture)}/remind", TokenFor(fixture.CoachId, fixture.Slug));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(0, body.GetProperty("reminded").GetInt32());
        Assert.False(outbox.Dispatches.TryDequeue(out _));
    }

    [Fact]
    public async Task Paminnelse_SomIckeTranare_Nekas()
    {
        var fixture = await SeedAsync("not-coach");
        var parent = await SeedSubscriberAsync(fixture, "Cecilia");
        var (app, _) = WithRecordingOutbox();

        var response = await PostAsync(app, $"{Base(fixture)}/remind", TokenFor(parent));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
