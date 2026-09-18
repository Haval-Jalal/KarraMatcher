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
