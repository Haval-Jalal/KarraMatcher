using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kalender-feeden bakom en privat nyckel (§KM.4, kalender bakom medlemskap).
///
/// <para>
/// En medlem hämtar sin länk (inloggad), och kalender-appen läser feeden anonymt via nyckeln.
/// Feeden bär medlemmens lag-händelser — inställda märkta, aldrig barn-PII. En okänd nyckel ger
/// 404, och en återkallad länk slutar fungera direkt.
/// </para>
/// </summary>
public sealed class CalendarFeedTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(Guid GuardianId);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-k-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-k-{suffix}",
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
            Type = EventType.Match,
            KickoffUtc = now.AddDays(3),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };
        var cancelled = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            Type = EventType.Match,
            KickoffUtc = now.AddDays(5),
            OpponentName = "Kungalv",
            VenueId = venue.Id,
            IsHome = false,
            Status = EventStatus.Cancelled,
            UpdatedUtc = now,
        };
        var guardian = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"vh-k-{suffix}@example.com",
            CreatedUtc = now,
        };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = trupp.Id,
            TeamId = team.Id,
            CreatedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.AddRange(match, cancelled);
        context.Accounts.Add(guardian);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(guardian.Id);
    }

    private string TokenFor(Guid accountId) =>
        TestAuth.TokenFor(factory.Services, accountId, AccountRoles.None, "konto@example.com");

    /// <summary>Plockar nyckeln ur kalender-URL:en (…/kalender/{token}.ics).</summary>
    private static string TokenFromUrl(string url)
    {
        const string marker = "/kalender/";
        var start = url.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = url.IndexOf(".ics", start, StringComparison.Ordinal);
        return url[start..end];
    }

    private async Task<string> MyTokenAsync(Guid accountId)
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/kalender/min");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(accountId));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return TokenFromUrl(body.GetProperty("url").GetString()!);
    }

    private async Task<HttpResponseMessage> FeedAsync(string token)
    {
        using var client = factory.CreateClient();
        return await client.GetAsync($"/api/v1/kalender/{token}.ics", CancellationToken.None);
    }

    [Fact]
    public async Task Feed_ForEnMedlemsNyckel_ListarLagetsHandelser_UtanBarnPii()
    {
        var f = await SeedAsync("feed");

        var token = await MyTokenAsync(f.GuardianId);
        var response = await FeedAsync(token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/calendar", response.Content.Headers.ContentType?.MediaType);

        var ics = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.Contains("BEGIN:VCALENDAR", ics, StringComparison.Ordinal);
        Assert.Contains("Gul – Hemma mot Torslanda", ics, StringComparison.Ordinal);
        Assert.Contains("STATUS:CANCELLED", ics, StringComparison.Ordinal); // den inställda
        Assert.DoesNotContain("Liam", ics, StringComparison.Ordinal); // aldrig barn-PII (§KM.1)
    }

    [Fact]
    public async Task Feed_OkandNyckel_Ger404()
    {
        var response = await FeedAsync("finns-inte");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Aterkalla_GorGamlaLankenOgiltig()
    {
        var f = await SeedAsync("revoke");

        var oldToken = await MyTokenAsync(f.GuardianId);

        var newToken = await RegenerateAsync(f.GuardianId);
        Assert.NotEqual(oldToken, newToken);

        Assert.Equal(HttpStatusCode.NotFound, (await FeedAsync(oldToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await FeedAsync(newToken)).StatusCode);
    }

    private async Task<string> RegenerateAsync(Guid accountId)
    {
        var token = TokenFor(accountId);

        using var client = factory.CreateClient(ClientOptions);

        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        csrfResponse.EnsureSuccessStatusCode();

        var csrfBody = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var csrf = csrfBody.GetProperty("token").GetString()!;
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/kalender/aterkalla");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        return TokenFromUrl(body.GetProperty("url").GetString()!);
    }
}
