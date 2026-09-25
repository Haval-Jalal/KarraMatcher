using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Common;
using KarraMatcher.Infrastructure.Persistence;
using KarraMatcher.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KarraMatcher.Infrastructure.Tests;

public class DatabaseSeederTests
{
    // Tom konfiguration: ingen SuperAdmin:Email satt, så superadmin-seeden hoppas över.
    private static readonly IConfiguration EmptyConfig = new ConfigurationBuilder().Build();

    private static KarraMatcherDbContext NewContext(string name) =>
        new(new DbContextOptionsBuilder<KarraMatcherDbContext>()
            .UseInMemoryDatabase(name)
            .Options);

    [Fact]
    public async Task Seed_EnKorning_LaggerInAllStartdata()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());

        var result = await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        Assert.Equal(4, result.Teams);
        Assert.Equal(7, result.Venues);
        Assert.Equal(25, result.MatchesAdded);
        Assert.Equal(1, await context.Clubs.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.AgeGroups.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Seed_TvaKorningar_GerSammaResultatSomEn()
    {
        // Seeden körs vid varje driftsättning. Vore den inte idempotent skulle
        // schemat dubbleras varje gång vi släpper en ny version.
        var name = Guid.NewGuid().ToString();

        await using (var first = NewContext(name))
        {
            await new DatabaseSeeder(first, EmptyConfig).SeedAsync(CancellationToken.None);
        }

        await using var second = NewContext(name);
        var result = await new DatabaseSeeder(second, EmptyConfig).SeedAsync(CancellationToken.None);

        Assert.Equal(0, result.MatchesAdded);
        Assert.Equal(1, await second.Clubs.CountAsync(CancellationToken.None));
        Assert.Equal(1, await second.AgeGroups.CountAsync(CancellationToken.None));
        Assert.Equal(4, await second.Teams.CountAsync(CancellationToken.None));
        Assert.Equal(7, await second.Venues.CountAsync(CancellationToken.None));
        Assert.Equal(25, await second.Events.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Seed_AllaSpelplatser_HarKoordinater()
    {
        // Koordinaterna driver väderprognosen. En spelplats utan dem ger ingen prognos.
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        var venues = await context.Venues.ToListAsync(CancellationToken.None);

        Assert.Equal(7, venues.Count);
        Assert.All(venues, v =>
        {
            Assert.InRange(v.Latitude, 55, 60);
            Assert.InRange(v.Longitude, 10, 15);
        });
    }

    [Fact]
    public async Task Seed_EndastKlarebergsvallen_ArHemmaplan()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        var home = await context.Venues.Where(v => v.IsHome)
            .ToListAsync(CancellationToken.None);

        Assert.Single(home);
        Assert.StartsWith("Klarebergsvallen", home[0].Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Seed_Matchtider_ArKonverteradeFranSvenskTidTillUtc()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        // Första matchen: 29 augusti kl 14.30 svensk tid = 12:30 UTC (sommartid).
        var first = await context.Events.OrderBy(m => m.KickoffUtc)
            .FirstAsync(CancellationToken.None);

        Assert.Equal(new DateTime(2026, 8, 29, 12, 30, 0, DateTimeKind.Utc), first.KickoffUtc);
        Assert.Equal("Finlandia Pallo AIF Vit", first.OpponentName);

        // Och tillbaka igen ska ge den tid tränaren skrev.
        Assert.Equal(
            new DateTime(2026, 8, 29, 14, 30, 0),
            SwedishTime.ToSwedish(first.KickoffUtc));
    }

    [Fact]
    public async Task Seed_AllaMatcher_HarUtcSomKind()
    {
        // Npgsql vägrar skriva en icke-UTC DateTime till timestamptz. Skulle någon
        // tid slinka igenom som lokal tid faller seeden mot en riktig databas.
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        var matches = await context.Events.ToListAsync(CancellationToken.None);

        Assert.Equal(25, matches.Count);
        Assert.All(matches, m => Assert.Equal(DateTimeKind.Utc, m.KickoffUtc.Kind));
    }

    [Fact]
    public async Task Seed_AllaLag_HarKallelsenAvstangd()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        var teams = await context.Teams.ToListAsync(CancellationToken.None);

        Assert.Equal(4, teams.Count);
        Assert.All(teams, t => Assert.False(t.AttendanceEnabled));
    }

    [Fact]
    public async Task Seed_SkaparSporten_OchKopplarTruppenTillDen()
    {
        // v2 (#190): en trupp hör till en sport. Fotboll seedas och P2016 pekar på den.
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        var sport = await context.Sports.SingleAsync(CancellationToken.None);
        Assert.Equal("fotboll", sport.Slug);

        var ageGroup = await context.AgeGroups.SingleAsync(CancellationToken.None);
        Assert.Equal(sport.Id, ageGroup.SportId);
    }

    [Fact]
    public async Task Seed_UtanSuperAdminEmail_SkaparIngenSuperAdmin()
    {
        // Ingen adress satt → ingen superadmin. Aldrig hårdkodad i repot.
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        Assert.Equal(0, await context.Accounts.CountAsync(CancellationToken.None));
        Assert.Equal(0, await context.TeamRoles.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Seed_MedSuperAdminEmail_SkaparKontoOchRoll_Idempotent()
    {
        // v2 (#190): superadmin seedas från konfig, en gång. En andra körning ändrar inget.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DatabaseSeeder.SuperAdminEmailKey] = "Agare@Example.com",
            })
            .Build();

        var name = Guid.NewGuid().ToString();

        await using (var first = NewContext(name))
        {
            await new DatabaseSeeder(first, config).SeedAsync(CancellationToken.None);
        }

        await using var context = NewContext(name);
        await new DatabaseSeeder(context, config).SeedAsync(CancellationToken.None);

        var account = await context.Accounts.SingleAsync(CancellationToken.None);
        Assert.Equal("agare@example.com", account.Email);

        var role = await context.TeamRoles.SingleAsync(CancellationToken.None);
        Assert.Equal(RoleKind.SuperAdmin, role.Role);
        Assert.Equal(account.Id, role.AccountId);
        Assert.Null(role.TeamId);
        Assert.Null(role.AgeGroupId);
    }

    // ---- Demodata (#267) -------------------------------------------------------------

    private static IConfiguration DemoConfig(
        bool enabled = true,
        bool clear = false,
        string admin = "Demo.Admin@Example.com",
        string guardian = "Demo.Vh@Example.com") =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [DatabaseSeeder.DemoEnabledKey] = enabled ? "true" : "false",
                [DatabaseSeeder.DemoClearKey] = clear ? "true" : "false",
                [DatabaseSeeder.DemoAdminEmailKey] = admin,
                [DatabaseSeeder.DemoGuardianEmailKey] = guardian,
            })
            .Build();

    [Fact]
    public async Task Seed_MedDemodata_SkaparAdminForalderMedSamtyckeOchBarn()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, DemoConfig()).SeedAsync(CancellationToken.None);

        // Admin: eget konto (adress normaliserad) med Admin-roll på truppen.
        var admin = await context.Accounts
            .SingleAsync(a => a.Email == "demo.admin@example.com", CancellationToken.None);
        var ageGroup = await context.AgeGroups.SingleAsync(CancellationToken.None);
        var adminRole = await context.TeamRoles
            .SingleAsync(r => r.AccountId == admin.Id, CancellationToken.None);
        Assert.Equal(RoleKind.Admin, adminRole.Role);
        Assert.Equal(ageGroup.Id, adminRole.AgeGroupId);

        // Vårdnadshavare: eget konto med samtycke till aktuell version (§KM.6).
        var guardian = await context.Accounts
            .SingleAsync(a => a.Email == "demo.vh@example.com", CancellationToken.None);
        var consent = await context.GuardianConsents
            .SingleAsync(gc => gc.AccountId == guardian.Id, CancellationToken.None);
        Assert.False(string.IsNullOrEmpty(consent.Version));

        // Tre barn på Gul, alla kopplade till vårdnadshavaren; barnen är minimala (§KM.1).
        var gul = await context.Teams.SingleAsync(t => t.Slug == "gul", CancellationToken.None);
        var children = await context.Children
            .Where(x => x.TeamId == gul.Id)
            .ToListAsync(CancellationToken.None);
        Assert.Equal(3, children.Count);
        Assert.All(children, x => Assert.False(string.IsNullOrEmpty(x.LastInitial)));

        var links = await context.Guardianships
            .CountAsync(g => g.AccountId == guardian.Id, CancellationToken.None);
        Assert.Equal(3, links);

        // Kallelsen är påslagen på demolaget så den går att prova.
        Assert.True(gul.AttendanceEnabled);
    }

    [Fact]
    public async Task Seed_Demodata_ArIdempotent()
    {
        var name = Guid.NewGuid().ToString();

        await using (var first = NewContext(name))
        {
            await new DatabaseSeeder(first, DemoConfig()).SeedAsync(CancellationToken.None);
        }

        await using var context = NewContext(name);
        await new DatabaseSeeder(context, DemoConfig()).SeedAsync(CancellationToken.None);

        Assert.Equal(2, await context.Accounts.CountAsync(CancellationToken.None));
        Assert.Equal(3, await context.Children.CountAsync(CancellationToken.None));
        Assert.Equal(3, await context.Guardianships.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.GuardianConsents.CountAsync(CancellationToken.None));
        Assert.Equal(1, await context.TeamRoles.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Seed_DemoClear_TarBortAllDemodata_OchStangerAvKallelsen()
    {
        var name = Guid.NewGuid().ToString();

        await using (var seeded = NewContext(name))
        {
            await new DatabaseSeeder(seeded, DemoConfig()).SeedAsync(CancellationToken.None);
        }

        await using var context = NewContext(name);
        await new DatabaseSeeder(context, DemoConfig(clear: true)).SeedAsync(CancellationToken.None);

        Assert.Equal(0, await context.Accounts.CountAsync(CancellationToken.None));
        Assert.Equal(0, await context.Children.CountAsync(CancellationToken.None));
        Assert.Equal(0, await context.Guardianships.CountAsync(CancellationToken.None));
        Assert.Equal(0, await context.GuardianConsents.CountAsync(CancellationToken.None));
        Assert.Equal(0, await context.TeamRoles.CountAsync(CancellationToken.None));

        var gul = await context.Teams.SingleAsync(t => t.Slug == "gul", CancellationToken.None);
        Assert.False(gul.AttendanceEnabled);
    }

    [Fact]
    public async Task Seed_UtanDemoEnabled_SkaparIngenDemodata()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, DemoConfig(enabled: false)).SeedAsync(CancellationToken.None);

        Assert.Equal(0, await context.Accounts.CountAsync(CancellationToken.None));
        Assert.Equal(0, await context.Children.CountAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Seed_VarjeLag_HarSinaMatcher()
    {
        await using var context = NewContext(Guid.NewGuid().ToString());
        await new DatabaseSeeder(context, EmptyConfig).SeedAsync(CancellationToken.None);

        var perTeam = await context.Teams
            .Select(t => new { t.Slug, Count = context.Events.Count(m => m.TeamId == t.Id) })
            .ToListAsync(CancellationToken.None);

        Assert.Equal(4, perTeam.Count);
        Assert.All(perTeam, t => Assert.True(t.Count > 0, $"Lag {t.Slug} saknar matcher"));
        Assert.Equal(25, perTeam.Sum(t => t.Count));
    }
}
