using System.Net;
using System.Net.Http.Headers;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Chat;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Anti-IDOR-matris: en medlem i en trupp når <b>aldrig</b> en annan trupps data (§KM.1/§KM.3).
///
/// <para>
/// Object-level authorization är nästa vanliga läckväg efter SQL-injektion: byt id:t i adressen
/// och läs någon annans. <see cref="AuthorizationTests"/> prövar policyn via en prob-controller;
/// den här prövar de <em>riktiga</em> object-adresserade endpoints från början till slut. Två
/// helt åtskilda truppar seedas, och trupp A:s vårdnadshavare — en vanlig medlem, roll via
/// vårdnadshavarskap i databasen, inte via token — riktas mot trupp B:s objekt. Varje sådan
/// begäran ska nekas (403/404), och samma endpoint mot A:s egna objekt ska lyckas (200), så att
/// nekandet bevisligen beror på ägarskapet och inte på att endpointen är trasig.
/// </para>
/// </summary>
public sealed class IdorTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Tenant(Guid TruppId, string TeamSlug, Guid EventId, Guid GuardianId);

    /// <summary>De object-adresserade medlems-endpoints matrisen prövar, som mall per trupp.</summary>
    private static readonly (string Label, Func<Tenant, string> Path)[] Endpoints =
    [
        ("händelsedetalj", t => $"/api/v1/events/{t.EventId}"),
        ("lagets schema", t => $"/api/v1/teams/{t.TeamSlug}/events"),
        ("truppens chatt-kanaler", t => $"/api/v1/trupper/{t.TruppId}/chat/channels"),
        ("truppens chatt-meddelanden", t => $"/api/v1/trupper/{t.TruppId}/chat/messages"),
        ("truppens cuper", t => $"/api/v1/trupper/{t.TruppId}/cups"),
    ];

    [Fact]
    public async Task Medlem_NarAldrigEnAnnanTruppsObjekt_MenSinaEgna()
    {
        var a = await SeedAsync("a");
        var b = await SeedAsync("b");

        var tokenA = TestAuth.TokenFor(factory.Services, a.GuardianId, AccountRoles.None, "a@example.com");

        var failures = new List<string>();

        foreach (var (label, path) in Endpoints)
        {
            // Eget objekt: ska lyckas — annars bevisar nekandet nedan ingenting.
            var own = await GetAsync(path(a), tokenA);

            if (own != HttpStatusCode.OK)
            {
                failures.Add($"{label}: eget objekt gav {(int)own} {own} (väntade 200)");
            }

            // Annans objekt: ska nekas. Policy-fel ger 403; en handler som döljer existens ger
            // 404 — båda är rätt svar, ett 200 är läckan.
            var other = await GetAsync(path(b), tokenA);

            if (other is not (HttpStatusCode.Forbidden or HttpStatusCode.NotFound))
            {
                failures.Add($"{label}: ANNANS objekt gav {(int)other} {other} (väntade 403/404 — IDOR!)");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"Object-level auktorisering brister:{Environment.NewLine}"
                + string.Join(Environment.NewLine, failures.Select(f => "  - " + f)));
    }

    private async Task<HttpStatusCode> GetAsync(string path, string token)
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, CancellationToken.None);
        return response.StatusCode;
    }

    private async Task<Tenant> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-i-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-i-{suffix}",
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
        var guardian = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"vh-i-{suffix}@example.com",
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
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = null,
            AuthorAccountId = guardian.Id,
            Body = "Hej truppen!",
            CreatedUtc = now,
            PublishAtUtc = now,
            PublishedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.Add(match);
        context.Accounts.Add(guardian);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Tenant(trupp.Id, team.Slug, match.Id, guardian.Id);
    }
}
