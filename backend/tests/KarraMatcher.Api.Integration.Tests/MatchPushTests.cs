using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Push vid matchändring (`#62`, §KM.1, §KM.2).
///
/// <para>
/// Notisen köas, aldrig skickas i requesten (§KM.11) — så det som prövas här är att rätt
/// notis läggs i kön vid rätt händelse, med en text som säger <em>vad</em> som ändrats och
/// utan något känsligt. Utkorgen är utbytt mot en attrapp som bara antecknar.
/// </para>
/// </summary>
public sealed class MatchPushTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    /// <summary>Utkorg som bara antecknar vad som köats.</summary>
    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private sealed record Fixture(string Slug, Guid TeamId, Guid MatchId, Guid VenueId, Guid OtherVenueId, Guid CoachId);

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

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-p-{suffix}" };
        var ageGroup = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-p-{suffix}",
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
        var other = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Skarpe Nord",
            Address = "Kongahallavagen, Kungalv",
            Latitude = 57.87,
            Longitude = 11.98,
            IsHome = false,
        };
        var match = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            KickoffUtc = now.AddDays(5),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = now,
        };
        var coach = new Account { Id = Guid.NewGuid(), Email = $"tranare-p-{suffix}@example.com", CreatedUtc = now };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.AddRange(venue, other);
        context.Events.Add(match);
        context.Accounts.Add(coach);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Slug, team.Id, match.Id, venue.Id, other.Id, coach.Id);
    }

    private string CoachToken(Guid accountId, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [], [slug])).Token;
    }

    private static async Task SignAsync(HttpClient client, HttpRequestMessage request, string token)
    {
        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        csrfResponse.EnsureSuccessStatusCode();

        var body = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", body.GetProperty("token").GetString()!);
        request.Headers.Add("Cookie", cookie);
    }

    private static object MatchBody(DateTime kickoffUtc, Guid venueId, bool isHome = true, string? note = null) =>
        new
        {
            type = "Match",
            kickoffUtc,
            opponent = "Torslanda",
            venueId,
            isHome,
            addressOverride = (string?)null,
            note,
        };

    // ---- Skapa / ändra / ställa in köar en notis -------------------------------------

    [Fact]
    public async Task NyMatch_KoarNotis()
    {
        var fixture = await SeedAsync("create");
        var (app, outbox) = WithRecordingOutbox();
        using var client = app.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/teams/{fixture.Slug}/events")
        {
            Content = JsonContent.Create(MatchBody(DateTime.UtcNow.AddDays(7), fixture.VenueId)),
        };
        await SignAsync(client, request, CoachToken(fixture.CoachId, fixture.Slug));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal(fixture.TeamId, dispatch.TeamId);
        Assert.Contains("Ny match", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FlyttadTid_KoarNyTidNotis()
    {
        var fixture = await SeedAsync("moved");
        var (app, outbox) = WithRecordingOutbox();
        using var client = app.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/teams/{fixture.Slug}/events/{fixture.MatchId}")
        {
            Content = JsonContent.Create(MatchBody(DateTime.UtcNow.AddDays(9), fixture.VenueId)),
        };
        await SignAsync(client, request, CoachToken(fixture.CoachId, fixture.Slug));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Contains("Ny tid", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NyPlats_KoarNyPlatsNotisMedNamnet()
    {
        var fixture = await SeedAsync("venue");
        var (app, outbox) = WithRecordingOutbox();
        using var client = app.CreateClient(ClientOptions);

        // Hämtar matchens nuvarande avspark så bara platsen ändras.
        DateTime kickoff;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
            kickoff = context.Events.Single(m => m.Id == fixture.MatchId).KickoffUtc;
        }

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/teams/{fixture.Slug}/events/{fixture.MatchId}")
        {
            Content = JsonContent.Create(MatchBody(kickoff, fixture.OtherVenueId)),
        };
        await SignAsync(client, request, CoachToken(fixture.CoachId, fixture.Slug));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Contains("Ny plats", dispatch.Message.Title, StringComparison.Ordinal);
        Assert.Contains("Skarpe Nord", dispatch.Message.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InstalldMatch_KoarNotis()
    {
        var fixture = await SeedAsync("cancel");
        var (app, outbox) = WithRecordingOutbox();
        using var client = app.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/teams/{fixture.Slug}/events/{fixture.MatchId}/cancel");
        await SignAsync(client, request, CoachToken(fixture.CoachId, fixture.Slug));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Contains("Inställt", dispatch.Message.Title, StringComparison.Ordinal);

        // Klick på notisen öppnar rätt match.
        Assert.Equal($"/handelse/{fixture.MatchId}", dispatch.Message.Url);
    }

    // ---- Det som inte ska köa en notis -----------------------------------------------

    [Fact]
    public async Task AndradNotisText_KoarIngenNotis()
    {
        // Bara tränarens notis ändras -- ingen tid, ingen plats. En förälder ska inte väckas
        // för det, och notisen (fritext) får aldrig lämna servern (§KM.1).
        var fixture = await SeedAsync("note-only");
        var (app, outbox) = WithRecordingOutbox();
        using var client = app.CreateClient(ClientOptions);

        DateTime kickoff;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
            kickoff = context.Events.Single(m => m.Id == fixture.MatchId).KickoffUtc;
        }

        var request = new HttpRequestMessage(
            HttpMethod.Put,
            $"/api/v1/teams/{fixture.Slug}/events/{fixture.MatchId}")
        {
            Content = JsonContent.Create(MatchBody(kickoff, fixture.VenueId, note: "Elias mamma kör kiosken")),
        };
        await SignAsync(client, request, CoachToken(fixture.CoachId, fixture.Slug));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        Assert.False(outbox.Dispatches.TryDequeue(out _));
    }

    [Fact]
    public async Task RaderadMatch_KoarIngenNotis()
    {
        // Radering är inte en av de fyra händelserna (#62), och en notis som pekar på en match
        // som inte längre finns hade lett till en 404.
        var fixture = await SeedAsync("delete");
        var (app, outbox) = WithRecordingOutbox();
        using var client = app.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/teams/{fixture.Slug}/events/{fixture.MatchId}");
        await SignAsync(client, request, CoachToken(fixture.CoachId, fixture.Slug));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        Assert.False(outbox.Dispatches.TryDequeue(out _));
    }

    [Fact]
    public async Task Notis_BarAldrigNagonPII()
    {
        // Notisen som skapas ska aldrig innehålla tränarens fritext, även när matchen skapas
        // med en notis full av sådant som inte får lämna servern (§KM.1, §KM.2).
        var fixture = await SeedAsync("no-pii");
        var (app, outbox) = WithRecordingOutbox();
        using var client = app.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/teams/{fixture.Slug}/events")
        {
            Content = JsonContent.Create(
                MatchBody(DateTime.UtcNow.AddDays(7), fixture.VenueId, note: "Elias har feber, ring mamma 070")),
        };
        await SignAsync(client, request, CoachToken(fixture.CoachId, fixture.Slug));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        var text = dispatch.Message.Title + " " + dispatch.Message.Body;
        Assert.DoesNotContain("Elias", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("feber", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("070", text, StringComparison.Ordinal);
    }
}
