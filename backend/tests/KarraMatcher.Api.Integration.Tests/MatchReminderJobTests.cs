using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Common;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kvällspåminnelsens schemalagda jobb (`#64`, §KM.11).
///
/// <para>
/// Prövar det som annars märks först i drift: att endpointen är stängd utan rätt hemlighet,
/// att bara morgondagens matcher tas med, att inställda inte påminner, och att en dubbel
/// körning inte ger dubbla notiser. Klockan är fastnaglad, så "i morgon" inte flyttar sig
/// mellan att testet sår och jobbet kör.
/// </para>
/// </summary>
public sealed class MatchReminderJobTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private const string Secret = "test-cron-secret-0123456789";

    // Fredag 18:00 UTC = 20:00 svensk sommartid. "I morgon" blir lördag 19 sept.
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 18, 18, 0, 0, TimeSpan.Zero);

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private (WebApplicationFactory<Program> App, RecordingOutbox Outbox) Configured(bool withSecret = true)
    {
        var outbox = new RecordingOutbox();

        var app = factory.WithWebHostBuilder(builder =>
        {
            if (withSecret)
            {
                builder.UseSetting("Jobs:Secret", Secret);
            }

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPushOutbox>();
                services.AddSingleton<IPushOutbox>(outbox);
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedClock(FixedNow));
            });
        });

        return (app, outbox);
    }

    private static DateOnly Tomorrow => DateOnly.FromDateTime(SwedishTime.ToSwedish(FixedNow.UtcDateTime)).AddDays(1);

    private async Task<Guid> SeedTeamAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-j-{suffix}" };
        var ageGroup = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-j-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        await context.SaveChangesAsync(CancellationToken.None);

        return team.Id;
    }

    private async Task<Guid> SeedMatchAsync(Guid teamId, DateOnly date, bool cancelled = false)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

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
            TeamId = teamId,
            KickoffUtc = SwedishTime.ToUtc(date, new TimeOnly(10, 0)),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = cancelled ? MatchStatus.Cancelled : MatchStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = FixedNow.UtcDateTime,
        };

        context.Venues.Add(venue);
        context.Matches.Add(match);
        await context.SaveChangesAsync(CancellationToken.None);

        return match.Id;
    }

    private static async Task<HttpResponseMessage> RunAsync(
        WebApplicationFactory<Program> app,
        string? bearer)
    {
        using var client = app.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/jobs/match-reminders");

        if (bearer is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Skydd -----------------------------------------------------------------------

    [Fact]
    public async Task UtanHemlighet_Nekas()
    {
        var (app, _) = Configured();

        var response = await RunAsync(app, bearer: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task FelHemlighet_Nekas()
    {
        var (app, _) = Configured();

        var response = await RunAsync(app, "fel-hemlighet");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OkonfigureradHemlighet_Nekas()
    {
        // Tom hemlighet stanger jobbet -- aven ett anrop som "ser ratt ut" avvisas.
        var (app, _) = Configured(withSecret: false);

        var response = await RunAsync(app, Secret);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Rätt matcher ----------------------------------------------------------------

    [Fact]
    public async Task MedRattHemlighet_PaminnerOmMorgondagensMatch()
    {
        var teamId = await SeedTeamAsync("tomorrow");
        await SeedMatchAsync(teamId, Tomorrow);
        var (app, outbox) = Configured();

        var response = await RunAsync(app, Secret);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(1, body.GetProperty("reminded").GetInt32());

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal(teamId, dispatch.TeamId);
        Assert.Contains("i morgon", dispatch.Message.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PaminnerBaraMorgondagens()
    {
        var teamId = await SeedTeamAsync("window");
        await SeedMatchAsync(teamId, Tomorrow.AddDays(-1)); // idag
        await SeedMatchAsync(teamId, Tomorrow);
        await SeedMatchAsync(teamId, Tomorrow.AddDays(1)); // i overmorgon
        var (app, _) = Configured();

        var response = await RunAsync(app, Secret);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(1, body.GetProperty("reminded").GetInt32());
    }

    [Fact]
    public async Task InstalldMatch_PaminnerInte()
    {
        var teamId = await SeedTeamAsync("cancelled");
        await SeedMatchAsync(teamId, Tomorrow, cancelled: true);
        var (app, outbox) = Configured();

        var response = await RunAsync(app, Secret);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(0, body.GetProperty("reminded").GetInt32());
        Assert.False(outbox.Dispatches.TryDequeue(out _));
    }

    [Fact]
    public async Task DubbelKorning_GerIngaDubblaNotiser()
    {
        var teamId = await SeedTeamAsync("idempotent");
        await SeedMatchAsync(teamId, Tomorrow);
        var (app, outbox) = Configured();

        var first = await RunAsync(app, Secret);
        var second = await RunAsync(app, Secret);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        Assert.Equal(1, firstBody.GetProperty("reminded").GetInt32());
        Assert.Equal(0, secondBody.GetProperty("reminded").GetInt32());

        Assert.True(outbox.Dispatches.TryDequeue(out _));
        Assert.False(outbox.Dispatches.TryDequeue(out _));
    }
}
