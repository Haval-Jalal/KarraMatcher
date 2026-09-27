using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Adminens skapande av händelser på trupp-nivå (`#332`). En admin kan lägga upp en
/// <b>trupp-vid</b> händelse (utan lag, hela truppen) eller en lag-riktad — och bara mot
/// truppens egna lag.
/// </summary>
public sealed class TruppEventAdminTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record Fixture(Guid TruppId, Guid TeamId);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club
        {
            Id = Guid.NewGuid(),
            Name = "Karra KIF",
            Slug = $"klubb-ta-{suffix}",
            HomeVenueName = "Karra IP",
            HomeAddress = "Idrottsvagen 1, Goteborg",
            HomeLatitude = 57.79,
            HomeLongitude = 11.94,
        };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-ta-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(trupp.Id, team.Id);
    }

    private string AdminToken(Guid truppId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer.Issue(Guid.NewGuid(), "admin@example.com", new AccountRoles(false, [truppId.ToString()], []))
            .Token;
    }

    private async Task<HttpResponseMessage> PostAsync(Guid truppId, object payload)
    {
        var token = AdminToken(truppId);
        using var client = factory.CreateClient(ClientOptions);

        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        csrfResponse.EnsureSuccessStatusCode();
        var csrfBody = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(v => v.StartsWith("karra_csrf", StringComparison.Ordinal)).Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/trupper/{truppId}/events")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrfBody.GetProperty("token").GetString());
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task<Event> LoadEventAsync(Guid eventId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        return await context.Events.AsNoTracking()
            .SingleAsync(e => e.Id == eventId, CancellationToken.None);
    }

    [Fact]
    public async Task Admin_SkaparTruppVidTraning_UtanLag()
    {
        var f = await SeedAsync("truppwide");

        var response = await PostAsync(f.TruppId, new
        {
            type = "Training",
            kickoffUtc = new DateTime(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc),
            title = "Trupp-traning",
            isHome = true,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var created = await LoadEventAsync(body.GetProperty("id").GetGuid());

        Assert.Null(created.TeamId); // trupp-vid — inget lag
        Assert.Equal(f.TruppId, created.AgeGroupId);
        Assert.Equal(EventType.Training, created.Type);
    }

    [Fact]
    public async Task Admin_SkaparLagRiktadMatch_MotEttEgetLag()
    {
        var f = await SeedAsync("teamtargeted");

        var response = await PostAsync(f.TruppId, new
        {
            type = "Match",
            kickoffUtc = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc),
            opponent = "Torslanda",
            isHome = true,
            teamId = f.TeamId,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var created = await LoadEventAsync(body.GetProperty("id").GetGuid());

        Assert.Equal(f.TeamId, created.TeamId);
        Assert.Equal(f.TruppId, created.AgeGroupId);
    }

    [Fact]
    public async Task Admin_MotEttLagUtanforTruppen_Nekas()
    {
        var f = await SeedAsync("outside");

        var response = await PostAsync(f.TruppId, new
        {
            type = "Match",
            kickoffUtc = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc),
            opponent = "Torslanda",
            isHome = true,
            teamId = Guid.NewGuid(), // finns inte i truppen
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
