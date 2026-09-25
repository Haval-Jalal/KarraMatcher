using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kanallistan (§KM.3, `#293`): vilka chatt-kanaler en medlem får se i en trupp — primärkanalen
/// ("Truppen") plus nåbara lag-kanaler. Vårdnadshavare ser sitt barns lag, admin/tränare ser
/// alla, ett nyskapat lag syns direkt (utan meddelanden), en icke-medlem nekas. Behörigheten
/// avgörs server-side; det här är det som gör en kanalväxlare möjlig.
/// </summary>
public sealed class ChatChannelsTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Fixture(
        Guid TruppId,
        Guid SvartId,
        Guid GulId,
        Guid AdminId,
        Guid SvartGuardianId,
        Guid UnassignedGuardianId,
        Guid NonMemberId);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        var truppId = Guid.NewGuid();
        var svartId = Guid.NewGuid();
        var gulId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var svartGuardianId = Guid.NewGuid();
        var unassignedGuardianId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-ch-{suffix}" };
        db.Clubs.Add(club);
        db.AgeGroups.Add(new AgeGroup
        {
            Id = truppId,
            ClubId = club.Id,
            Name = "P2016",
            Season = "2026",
        });
        db.Teams.AddRange(
            new Team
            {
                Id = svartId,
                AgeGroupId = truppId,
                Name = "Svart",
                ColorHex = "#161616",
                Slug = $"svart-ch-{suffix}",
            },
            new Team
            {
                Id = gulId,
                AgeGroupId = truppId,
                Name = "Gul",
                ColorHex = "#D9A21B",
                Slug = $"gul-ch-{suffix}",
            });

        db.Accounts.AddRange(
            Acct(adminId, $"admin-{suffix}"),
            Acct(svartGuardianId, $"vh-svart-{suffix}"),
            Acct(unassignedGuardianId, $"vh-utan-{suffix}"),
            Acct(nonMemberId, $"utom-{suffix}"));

        // Admin för hela truppen (den trupp-breda tränarrollen efter #285).
        db.TeamRoles.Add(new TeamRole
        {
            Id = Guid.NewGuid(),
            AccountId = adminId,
            AgeGroupId = truppId,
            Role = RoleKind.Admin,
            GrantedUtc = now,
        });

        // En vårdnadshavare med barn i Svart, och en vars barn ännu inte tilldelats ett lag.
        AddGuardianChild(db, truppId, svartId, svartGuardianId, now);
        AddGuardianChild(db, truppId, null, unassignedGuardianId, now);

        await db.SaveChangesAsync(CancellationToken.None);

        return new Fixture(
            truppId, svartId, gulId, adminId, svartGuardianId, unassignedGuardianId, nonMemberId);
    }

    private static Account Acct(Guid id, string tag) =>
        new()
        {
            Id = id,
            Email = $"{tag}@example.com",
            FirstName = "X",
            CreatedUtc = DateTime.UtcNow,
        };

    private static void AddGuardianChild(
        KarraMatcherDbContext db, Guid truppId, Guid? teamId, Guid accountId, DateTime now)
    {
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = truppId,
            TeamId = teamId,
            CreatedUtc = now,
        };
        db.Children.Add(child);
        db.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ChildId = child.Id,
            GrantedUtc = now,
        });
    }

    private string Token(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", AccountRoles.None).Token;
    }

    private async Task<HttpResponseMessage> ChannelsResponseAsync(Guid truppId, Guid accountId)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/v1/trupper/{truppId}/chat/channels");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(accountId));

        return await client.SendAsync(request, CancellationToken.None);
    }

    private async Task<IReadOnlyList<JsonElement>> ChannelsAsync(Guid truppId, Guid accountId)
    {
        var response = await ChannelsResponseAsync(truppId, accountId);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return [.. body.EnumerateArray().Select(e => e.Clone())];
    }

    private static string Kind(JsonElement channel) => channel.GetProperty("kind").GetString()!;

    private static string Name(JsonElement channel) => channel.GetProperty("name").GetString()!;

    private static bool HasTeam(IEnumerable<JsonElement> channels, string name) =>
        channels.Any(c => Kind(c) == "Team" && Name(c) == name);

    // ---- Admin/tränare ser alla lag --------------------------------------------------

    [Fact]
    public async Task Admin_SerPrimarkanalenOchAllaLag()
    {
        var f = await SeedAsync("admin");

        var channels = await ChannelsAsync(f.TruppId, f.AdminId);

        // Primärkanalen först, sedan varje lag i truppen.
        Assert.Equal("Trupp", Kind(channels[0]));
        Assert.Equal("Truppen", Name(channels[0]));
        Assert.True(HasTeam(channels, "Svart"));
        Assert.True(HasTeam(channels, "Gul"));
        Assert.Equal(3, channels.Count);
    }

    // ---- Vårdnadshavare ser bara sitt barns lag --------------------------------------

    [Fact]
    public async Task Vardnadshavare_SerPrimarkanalenOchSittBarnsLag()
    {
        var f = await SeedAsync("vh");

        var channels = await ChannelsAsync(f.TruppId, f.SvartGuardianId);

        Assert.Equal("Trupp", Kind(channels[0]));
        Assert.True(HasTeam(channels, "Svart"));
        Assert.False(HasTeam(channels, "Gul")); // inte medlem av Gul
        Assert.Equal(2, channels.Count);

        // Lag-kanalen bär lagets slug och färg, så växlaren kan länka och färglägga.
        var svart = channels.Single(c => Kind(c) == "Team");
        Assert.Equal($"svart-ch-vh", svart.GetProperty("slug").GetString());
        Assert.Equal("#161616", svart.GetProperty("colorHex").GetString());
        Assert.Equal(f.SvartId, svart.GetProperty("teamId").GetGuid());
    }

    // ---- Trupp-medlem utan tilldelat lag ---------------------------------------------

    [Fact]
    public async Task Vardnadshavare_UtanTilldelatLag_SerBaraPrimarkanalen()
    {
        // Barnet hör till truppen men har inget färg-lag än — då finns bara primärkanalen.
        var f = await SeedAsync("utan-lag");

        var channels = await ChannelsAsync(f.TruppId, f.UnassignedGuardianId);

        Assert.Single(channels);
        Assert.Equal("Trupp", Kind(channels[0]));
    }

    // ---- Ett nyskapat lag syns direkt, utan meddelanden ------------------------------

    [Fact]
    public async Task NyttLag_SynsDirekt_UtanMeddelanden()
    {
        var f = await SeedAsync("nytt-lag");

        var before = await ChannelsAsync(f.TruppId, f.AdminId);
        Assert.False(HasTeam(before, "Blå"));

        // Ett nytt lag skapas — ingen chatt-kanal-åtgärd, inga meddelanden.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
            db.Teams.Add(new Team
            {
                Id = Guid.NewGuid(),
                AgeGroupId = f.TruppId,
                Name = "Blå",
                ColorHex = "#1E3F8A",
                Slug = "bla-ch-nytt-lag",
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var after = await ChannelsAsync(f.TruppId, f.AdminId);
        Assert.True(HasTeam(after, "Blå"));
        Assert.Equal(before.Count + 1, after.Count);
    }

    // ---- Icke-medlem nekas -----------------------------------------------------------

    [Fact]
    public async Task IckeMedlem_Nekas()
    {
        var f = await SeedAsync("icke-medlem");

        var response = await ChannelsResponseAsync(f.TruppId, f.NonMemberId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
