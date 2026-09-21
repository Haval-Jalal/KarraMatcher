using KarraMatcher.Application.Features.Chat;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Chat;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Gallringen av chatt (§KM.10, `#201`). Meddelanden äldre än 90 dagar tas bort med sina
/// anmälningar; nyare lämnas kvar. Ett löfte om radering som ingen prövar slutar tyst hållas.
/// </summary>
public sealed class ChatRetentionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Seeded(Guid OldId, Guid OldReportId, Guid FreshId);

    private async Task<Seeded> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-chatg-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var author = new Account { Id = Guid.NewGuid(), Email = $"a-chatg-{suffix}@example.com", CreatedUtc = now };
        var reporter = new Account { Id = Guid.NewGuid(), Email = $"r-chatg-{suffix}@example.com", CreatedUtc = now };

        // 91 dagar: strax bortom gränsen; 89 dagar: strax innanför.
        var old = Message(trupp.Id, author.Id, "Gammalt", now.AddDays(-91));
        var fresh = Message(trupp.Id, author.Id, "Nytt", now.AddDays(-89));
        var report = new ChatReport
        {
            Id = Guid.NewGuid(),
            MessageId = old.Id,
            ReportedByAccountId = reporter.Id,
            CreatedUtc = now.AddDays(-91),
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Accounts.AddRange(author, reporter);
        context.ChatMessages.AddRange(old, fresh);
        context.ChatReports.Add(report);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Seeded(old.Id, report.Id, fresh.Id);
    }

    private static ChatMessage Message(Guid truppId, Guid authorId, string body, DateTime publishAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgeGroupId = truppId,
            TeamId = null,
            AuthorAccountId = authorId,
            Body = body,
            CreatedUtc = publishAt,
            PublishAtUtc = publishAt,
            PublishedUtc = publishAt,
        };

    private async Task<int> PurgeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<ChatRetentionService>();

        return await retention.PurgeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Gallring_TarBortGamlaMeddelandenOchDerasAnmalningar()
    {
        var seeded = await SeedAsync("bort");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Null(await context.ChatMessages.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == seeded.OldId, CancellationToken.None));
        Assert.Null(await context.ChatReports.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == seeded.OldReportId, CancellationToken.None));
    }

    [Fact]
    public async Task Gallring_LamnarKvarDetSomInteNattGransen()
    {
        var seeded = await SeedAsync("kvar");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.NotNull(await context.ChatMessages.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == seeded.FreshId, CancellationToken.None));
    }

    [Fact]
    public async Task Gallring_GarAttKoraTvaGanger()
    {
        await SeedAsync("upprepning");

        await PurgeAsync();
        var second = await PurgeAsync();

        Assert.Equal(0, second);
    }
}
