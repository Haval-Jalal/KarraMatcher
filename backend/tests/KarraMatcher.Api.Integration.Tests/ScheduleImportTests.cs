using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Massinlägg: förhandsgranskning och import (`#39`).
///
/// <para>
/// Ingen ska behöva lita på en parser i blindo. Granskningen är det som gör funktionen
/// trygg nog att användas — och det som gör att en tränare vågar klistra in tjugofem rader
/// i stället för att knappa in dem.
/// </para>
/// </summary>
public sealed class ScheduleImportTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed record World(string GulSlug, string BlaSlug, Guid Actor);

    private async Task<World> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-{suffix}" };
        var ageGroup = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "P2016",
            Season = "2026",
        };

        var gul = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = $"Gul {suffix}",
            ColorHex = "#D9A21B",
            Slug = $"gul-{suffix}",
        };
        var bla = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = $"Bla {suffix}",
            ColorHex = "#1E3F8A",
            Slug = $"bla-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.AddRange(gul, bla);
        context.Venues.Add(new Venue
        {
            Id = Guid.NewGuid(),
            Name = $"Klarebergsvallen {suffix}",
            Address = "Klarebergsvallen, Goteborg",
            Latitude = 57.78,
            Longitude = 11.96,
            IsHome = true,
        });

        await context.SaveChangesAsync(CancellationToken.None);

        return new World(gul.Slug, bla.Slug, Guid.NewGuid());
    }

    private static string Pasted(string suffix, string teamName) =>
        $"Datum\tTid\tLag\tMotståndare\tPlats\n"
        + $"2026-09-05\t15:30\t{teamName}\tLundby IF\tKlarebergsvallen {suffix}\n"
        + $"2026-09-12\t13:15\t{teamName}\tKareby IS\tKlarebergsvallen {suffix}\n";

    private async Task<JsonElement> PostAsync(string path, string slug, Guid actor, string? text)
    {
        var token = TokenFor(actor, slug);

        using var client = factory.CreateClient(ClientOptions);

        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        var csrfBody = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { text }),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrfBody.GetProperty("token").GetString());
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
    }

    private string TokenFor(Guid actor, string slug)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(actor, "tranare@example.com", new AccountRoles(false, [slug])).Token;
    }

    /// <summary>Utfall som inte är fel: klara rader och överhoppade.</summary>
    private static readonly string[] HarmlessOutcomes = ["Ok", "Skipped"];

    private static string[] Outcomes(JsonElement result) =>
        [.. result.GetProperty("lines").EnumerateArray()
            .Select(line => line.GetProperty("outcome").GetString() ?? string.Empty)];

    // ---- Granskningen sparar ingenting ------------------------------------------------

    [Fact]
    public async Task Granskning_SparaIngenting()
    {
        // Hela poängen: tränaren ska se vad som blir av innan något händer.
        var world = await SeedAsync("granska");

        await PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import/preview",
            world.GulSlug,
            world.Actor,
            Pasted("granska", $"Gul granska"));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Empty(await context.Matches
            .Where(m => m.Team!.Slug == world.GulSlug)
            .ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Granskning_MarkerarVarjeRad()
    {
        var world = await SeedAsync("markera");

        var result = await PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import/preview",
            world.GulSlug,
            world.Actor,
            Pasted("markera", "Gul markera"));

        /*
         * Rubrikraden och den tomma sista raden hoppas over; de tva matchraderna ar klara.
         *
         * Testet raknar utfallen i stallet for att lista dem i ordning -- en avslutande
         * radbrytning i inklistringen ska inte kunna falla ett test om nagot helt annat.
         */
        var outcomes = Outcomes(result);

        Assert.Equal(2, outcomes.Count(outcome => outcome == "Ok"));
        Assert.All(outcomes, outcome => Assert.Contains(outcome, HarmlessOutcomes));
    }

    // ---- Importen sparar, men bara det egna laget -------------------------------------

    [Fact]
    public async Task Import_SparaRaderna()
    {
        var world = await SeedAsync("import");

        var result = await PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import",
            world.GulSlug,
            world.Actor,
            Pasted("import", "Gul import"));

        Assert.Equal(2, result.GetProperty("imported").GetInt32());

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Equal(2, await context.Matches
            .CountAsync(m => m.Team!.Slug == world.GulSlug, CancellationToken.None));
    }

    [Fact]
    public async Task Import_HoppaOverAndraLagsRader()
    {
        /*
         * Ett inklistrat serieschema innehaller ofta alla fyra lagen, och behorigheten
         * galler ett. Utan den har kontrollen kunde en tranare for Gul lagga upp matcher
         * i Bla genom att klistra in hela serien -- forbi policyn, som bara sett pa slugen
         * i adressen.
         */
        var world = await SeedAsync("annat");

        var result = await PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import",
            world.GulSlug,
            world.Actor,
            Pasted("annat", "Bla annat"));

        Assert.Equal(0, result.GetProperty("imported").GetInt32());
        Assert.Contains("OtherTeam", Outcomes(result));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Empty(await context.Matches
            .Where(m => m.Team!.Slug == world.BlaSlug)
            .ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Import_DelvisNarEnRadArTrasig()
    {
        // En trasig rad ska inte hindra de som är rätt. Tränaren rättar den för hand.
        var world = await SeedAsync("delvis");

        var text = Pasted("delvis", "Gul delvis") + "det här är inte en matchrad\n";

        var result = await PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import",
            world.GulSlug,
            world.Actor,
            text);

        Assert.Equal(2, result.GetProperty("imported").GetInt32());
        Assert.Contains("Incomplete", Outcomes(result));
    }

    [Fact]
    public async Task Import_TvaGanger_GerDubbletterAndraGangen()
    {
        // Ett schema inklistrat två gånger är ett vanligare misstag än man tror.
        var world = await SeedAsync("dubblett");
        var text = Pasted("dubblett", "Gul dubblett");

        await PostAsync($"/api/v1/teams/{world.GulSlug}/matches/import", world.GulSlug, world.Actor, text);

        var second = await PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import", world.GulSlug, world.Actor, text);

        Assert.Equal(0, second.GetProperty("imported").GetInt32());
        Assert.Equal(2, Outcomes(second).Count(outcome => outcome == "Duplicate"));
    }

    [Fact]
    public async Task Import_SkrivsTidenOmTillUtc()
    {
        // 15:30 svensk sommartid är 13:30 UTC. Skrevs den rakt av låg matchen två timmar
        // fel, och felet hade synts först i föräldrarnas kalendrar (§KM.5).
        var world = await SeedAsync("tid");

        await PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import",
            world.GulSlug,
            world.Actor,
            Pasted("tid", "Gul tid"));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var first = await context.Matches
            .Where(m => m.Team!.Slug == world.GulSlug)
            .OrderBy(m => m.KickoffUtc)
            .FirstAsync(CancellationToken.None);

        Assert.Equal(new DateTime(2026, 9, 5, 13, 30, 0, DateTimeKind.Utc), first.KickoffUtc);
    }

    [Fact]
    public async Task Import_KravarInloggning()
    {
        var world = await SeedAsync("anonym");

        using var client = factory.CreateClient(ClientOptions);

        var response = await client.PostAsync(
            $"/api/v1/teams/{world.GulSlug}/matches/import", null, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
