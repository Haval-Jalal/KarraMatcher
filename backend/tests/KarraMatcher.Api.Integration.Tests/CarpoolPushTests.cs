using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Push vid samåkningshändelser (`#63`, §KM.12, §KM.10).
///
/// <para>
/// Nytt erbjudande når laget; en ny förfrågan når föraren; ett svar når den som frågade; ett
/// tillbakadraget erbjudande når dem som accepterats. Ingen fritext följer med — förarens
/// notis, den frågandes hälsning och nekandets meddelande når aldrig en låsskärm.
/// </para>
/// </summary>
public sealed class CarpoolPushTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private sealed record Fixture(Guid TeamId, Guid MatchId, Guid DriverId, Guid RequesterId);

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

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-c-{suffix}" };
        var ageGroup = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-c-{suffix}",
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
            KickoffUtc = now.AddDays(5),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = MatchStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = now,
        };
        var driver = new Account { Id = Guid.NewGuid(), Email = $"forare-c-{suffix}@example.com", CreatedUtc = now };
        var requester = new Account { Id = Guid.NewGuid(), Email = $"fragare-c-{suffix}@example.com", CreatedUtc = now };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Matches.Add(match);
        context.Accounts.AddRange(driver, requester);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(team.Id, match.Id, driver.Id, requester.Id);
    }

    private async Task<Guid> SeedOfferAsync(Fixture fixture, string? note = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var offer = new CarpoolOffer
        {
            Id = Guid.NewGuid(),
            MatchId = fixture.MatchId,
            DriverAccountId = fixture.DriverId,
            Direction = CarpoolDirection.ToMatch,
            DeparturePlace = "Karra centrum",
            DepartureUtc = now.AddDays(5),
            Seats = 3,
            Note = note,
            Status = CarpoolOfferStatus.Open,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        context.CarpoolOffers.Add(offer);
        await context.SaveChangesAsync(CancellationToken.None);

        return offer.Id;
    }

    private async Task<Guid> SeedRequestAsync(Fixture fixture, Guid offerId, CarpoolRequestStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var request = new CarpoolRequest
        {
            Id = Guid.NewGuid(),
            OfferId = offerId,
            RequesterAccountId = fixture.RequesterId,
            Seats = 1,
            Message = null,
            Status = status,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        context.CarpoolRequests.Add(request);
        await context.SaveChangesAsync(CancellationToken.None);

        return request.Id;
    }

    private string TokenFor(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [])).Token;
    }

    private static async Task<HttpResponseMessage> PostAsync(
        WebApplicationFactory<Program> app,
        string path,
        string token,
        object? body = null)
    {
        using var client = app.CreateClient(ClientOptions);

        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        csrfResponse.EnsureSuccessStatusCode();

        var csrfBody = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrfBody.GetProperty("token").GetString()!);
        request.Headers.Add("Cookie", cookie);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    private static string Base(Guid matchId) => $"/api/v1/matches/{matchId}/carpool";

    [Fact]
    public async Task NyttErbjudande_NarLaget()
    {
        var fixture = await SeedAsync("offer");
        var (app, outbox) = WithRecordingOutbox();

        var response = await PostAsync(
            app,
            $"{Base(fixture.MatchId)}/offers",
            TokenFor(fixture.DriverId),
            new
            {
                direction = "ToMatch",
                departurePlace = "Karra centrum",
                departureUtc = DateTime.UtcNow.AddDays(5),
                seats = 3,
                note = (string?)null,
            });
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal(fixture.TeamId, dispatch.TeamId);
        Assert.Null(dispatch.AccountIds);
        Assert.Contains("Ny samåkning", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NyForfragan_NarForaren()
    {
        var fixture = await SeedAsync("request");
        var offerId = await SeedOfferAsync(fixture);
        var (app, outbox) = WithRecordingOutbox();

        var response = await PostAsync(
            app,
            $"{Base(fixture.MatchId)}/offers/{offerId}/requests",
            TokenFor(fixture.RequesterId),
            new { seats = 1, message = (string?)null });
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal(fixture.TeamId, dispatch.TeamId);
        Assert.Equal([fixture.DriverId], dispatch.AccountIds);
        Assert.Contains("Ny åkförfrågan", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Svar_NarDenSomFragade()
    {
        var fixture = await SeedAsync("answer");
        var offerId = await SeedOfferAsync(fixture);
        var requestId = await SeedRequestAsync(fixture, offerId, CarpoolRequestStatus.Pending);
        var (app, outbox) = WithRecordingOutbox();

        var response = await PostAsync(
            app,
            $"{Base(fixture.MatchId)}/requests/{requestId}/accept",
            TokenFor(fixture.DriverId),
            new { message = (string?)null });
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal([fixture.RequesterId], dispatch.AccountIds);
        Assert.Contains("Svar", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TillbakadragetErbjudande_NarDeAccepterade()
    {
        var fixture = await SeedAsync("withdraw");
        var offerId = await SeedOfferAsync(fixture);
        await SeedRequestAsync(fixture, offerId, CarpoolRequestStatus.Accepted);
        var (app, outbox) = WithRecordingOutbox();

        var response = await PostAsync(
            app,
            $"{Base(fixture.MatchId)}/offers/{offerId}/withdraw",
            TokenFor(fixture.DriverId));
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal([fixture.RequesterId], dispatch.AccountIds);
        Assert.Contains("drogs tillbaka", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Notis_BarAldrigNagonFritext()
    {
        // Den frågandes hälsning är fritext (§KM.12) och får aldrig nå förarens låsskärm.
        var fixture = await SeedAsync("no-text");
        var offerId = await SeedOfferAsync(fixture);
        var (app, outbox) = WithRecordingOutbox();

        var response = await PostAsync(
            app,
            $"{Base(fixture.MatchId)}/offers/{offerId}/requests",
            TokenFor(fixture.RequesterId),
            new { seats = 1, message = "Elias mamma ringer, 070-1234567" });
        response.EnsureSuccessStatusCode();

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        var text = dispatch.Message.Title + " " + dispatch.Message.Body;
        Assert.DoesNotContain("Elias", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("070", text, StringComparison.Ordinal);
    }
}
