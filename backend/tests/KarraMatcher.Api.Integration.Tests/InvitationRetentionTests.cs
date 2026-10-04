using KarraMatcher.Application.Features.Invitations;
using KarraMatcher.Domain.Invitations;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Gallringen av döda inbjudningar (§KM.6, `#585`). En inbjudan bär adressen den skickades
/// till; en utgången eller återkallad inbjudan som passerat gränsen tas bort, en giltig eller
/// nyligen utgången lämnas, och en accepterad (ett medlemskap) rörs aldrig. Ett löfte om
/// radering som ingen prövar slutar tyst hållas.
/// </summary>
public sealed class InvitationRetentionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Seeded(
        Guid ExpiredPendingId,
        Guid RevokedId,
        Guid WithinGraceId,
        Guid ValidPendingId,
        Guid AcceptedId);

    private async Task<Seeded> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-invg-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };

        // 31 dagar bortom utgången: strax förbi gränsen (30 d). 29 dagar: strax innanför.
        var expiredPending = Invite(trupp.Id, suffix, now.AddDays(-31), InvitationStatus.Pending);
        var revoked = Invite(trupp.Id, suffix, now.AddDays(-31), InvitationStatus.Revoked);
        var withinGrace = Invite(trupp.Id, suffix, now.AddDays(-29), InvitationStatus.Pending);
        var validPending = Invite(trupp.Id, suffix, now.AddDays(7), InvitationStatus.Pending);
        var accepted = Invite(trupp.Id, suffix, now.AddDays(-31), InvitationStatus.Accepted);

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Invitations.AddRange(expiredPending, revoked, withinGrace, validPending, accepted);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Seeded(
            expiredPending.Id, revoked.Id, withinGrace.Id, validPending.Id, accepted.Id);
    }

    private static Invitation Invite(Guid truppId, string suffix, DateTime expiresUtc, InvitationStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgeGroupId = truppId,
            Email = $"{Guid.NewGuid():N}-{suffix}@example.com",
            TokenHash = new string('c', 64),
            CreatedByAccountId = Guid.NewGuid(),
            CreatedUtc = expiresUtc.AddDays(-7),
            ExpiresUtc = expiresUtc,
            Status = status,
        };

    private async Task<int> PurgeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<InvitationRetentionService>();

        return await retention.PurgeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Gallring_TarBortUtgangnaOchAterkallade()
    {
        var seeded = await SeedAsync("bort");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Null(await context.Invitations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == seeded.ExpiredPendingId, CancellationToken.None));
        Assert.Null(await context.Invitations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == seeded.RevokedId, CancellationToken.None));
    }

    [Fact]
    public async Task Gallring_LamnarKvarGiltigaOchNyligenUtgangna()
    {
        var seeded = await SeedAsync("kvar");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.NotNull(await context.Invitations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == seeded.ValidPendingId, CancellationToken.None));
        Assert.NotNull(await context.Invitations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == seeded.WithinGraceId, CancellationToken.None));
    }

    [Fact]
    public async Task Gallring_RorAldrigAccepterade()
    {
        // En accepterad inbjudan är ett medlemskap och hör till ett levande konto — den
        // kaskaderar bort med kontot, inte med en städning av döda inbjudningar.
        var seeded = await SeedAsync("accepterad");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.NotNull(await context.Invitations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == seeded.AcceptedId, CancellationToken.None));
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
