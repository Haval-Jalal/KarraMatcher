using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Hemmaplanen hör till truppen, inte klubben (`#405`). Två trupper i samma klubb har oberoende
/// hemmaplaner: den ena truppens plan syns aldrig för den andra, och en admin når bara sin egen.
/// </summary>
public sealed class ClubVenueIsolationTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Fixture(Guid TruppMedPlan, Guid TruppUtanPlan);

    private async Task<Fixture> SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = "klubb-cv-iso" };
        var sportId = Guid.NewGuid();

        var withVenue = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            SportId = sportId,
            Name = "P2016",
            Season = "2026",
            HomeVenueName = "Karra IP",
            HomeAddress = "Idrottsvagen 1, Goteborg",
            HomeLatitude = 57.79,
            HomeLongitude = 11.94,
        };
        // Samma klubb, men ingen egen plan satt.
        var withoutVenue = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            SportId = sportId,
            Name = "P2014",
            Season = "2026",
        };

        context.Clubs.Add(club);
        context.Sports.Add(new Sport { Id = sportId, Name = "Fotboll", Slug = "fotboll" });
        context.AgeGroups.AddRange(withVenue, withoutVenue);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(withVenue.Id, withoutVenue.Id);
    }

    private string AdminToken(Guid truppId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer
            .Issue(Guid.NewGuid(), "admin@example.com", new AccountRoles(false, [truppId.ToString()], []))
            .Token;
    }

    private async Task<JsonElement> GetVenueAsync(Guid truppId)
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/admin/trupper/{truppId}/club-venue");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", AdminToken(truppId));

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None)).Clone();
    }

    [Fact]
    public async Task Hemmaplan_ArPerTrupp_InteDeladInomKlubben()
    {
        var f = await SeedAsync();

        // Truppen med en satt plan ser sin egen.
        var withVenue = await GetVenueAsync(f.TruppMedPlan);
        Assert.True(withVenue.GetProperty("configured").GetBoolean());
        Assert.Equal("Karra IP", withVenue.GetProperty("name").GetString());

        // Den andra truppen i SAMMA klubb ser ingen plan — den delas inte längre (#405).
        var withoutVenue = await GetVenueAsync(f.TruppUtanPlan);
        Assert.False(withoutVenue.GetProperty("configured").GetBoolean());
    }
}
