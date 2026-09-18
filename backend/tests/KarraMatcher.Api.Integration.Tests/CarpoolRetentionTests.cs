using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Carpool;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Gallringen av samåkning (`#55`, §KM.12, säkerhetschecklistan 9.8).
///
/// <para>
/// Löftet till föräldrarna är att hela matchens samåkning försvinner trettio dagar efter
/// att den spelats. Ett löfte om radering som ingen prövar är ett löfte som tyst slutar
/// hållas — det räcker med en främmandenyckel som stoppar en sats.
/// </para>
/// </summary>
public sealed class CarpoolRetentionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Seeded(Guid OldOfferId, Guid OldRequestId, Guid FreshOfferId, Guid FreshRequestId);

    /// <summary>Två matcher: en långt bortom gränsen och en nyss spelad.</summary>
    private async Task<Seeded> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-g-{suffix}" };
        var ageGroup = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "P2016",
            Season = "2026",
        };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-g-{suffix}",
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

        // Trettioen dagar: strax bortom gransen, inte langt bortom. Ett test som lagger
        // matchen ett halvar tillbaka hade gatt igenom aven med fel granstal.
        var expired = Match(team.Id, venue.Id, now.AddDays(-31));
        var fresh = Match(team.Id, venue.Id, now.AddDays(-29));

        var driver = new Account { Id = Guid.NewGuid(), Email = $"forare-g-{suffix}@example.com" };
        var asker = new Account { Id = Guid.NewGuid(), Email = $"fragare-g-{suffix}@example.com" };

        var oldOffer = Offer(expired.Id, driver.Id, now);
        var freshOffer = Offer(fresh.Id, driver.Id, now);

        var oldRequest = Request(oldOffer.Id, asker.Id, now);
        var freshRequest = Request(freshOffer.Id, asker.Id, now);

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.AddRange(expired, fresh);
        context.Accounts.AddRange(driver, asker);
        context.CarpoolOffers.AddRange(oldOffer, freshOffer);
        context.CarpoolRequests.AddRange(oldRequest, freshRequest);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Seeded(oldOffer.Id, oldRequest.Id, freshOffer.Id, freshRequest.Id);
    }

    private static Event Match(Guid teamId, Guid venueId, DateTime kickoffUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            KickoffUtc = kickoffUtc,
            OpponentName = "Torslanda",
            VenueId = venueId,
            IsHome = false,
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = kickoffUtc,
        };

    private static CarpoolOffer Offer(Guid matchId, Guid driverId, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            DriverAccountId = driverId,
            Direction = CarpoolDirection.Both,
            DeparturePlace = "Karra centrum",
            DepartureUtc = now,
            Seats = 3,
            Note = "Ring mig om ni behover skjuts",
            Status = CarpoolOfferStatus.Open,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

    private static CarpoolRequest Request(Guid offerId, Guid accountId, DateTime now) =>
        new()
        {
            Id = Guid.NewGuid(),
            OfferId = offerId,
            RequesterAccountId = accountId,
            Seats = 1,
            Message = "Vi bor vid Skogomevagen",
            Status = CarpoolRequestStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

    private async Task<CarpoolPurgeResult> PurgeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<CarpoolRetentionService>();

        return await retention.PurgeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Gallring_TarBortSamakningForSpeladeMatcher()
    {
        var seeded = await SeedAsync("borttagning");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Null(await context.CarpoolOffers.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == seeded.OldOfferId, CancellationToken.None));

        Assert.Null(await context.CarpoolRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == seeded.OldRequestId, CancellationToken.None));
    }

    [Fact]
    public async Task Gallring_LamnarKvarDetSomInteNattGransen()
    {
        // En match som spelades i forrgar ar fortfarande nagons pagaende overenskommelse.
        var seeded = await SeedAsync("kvar");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.NotNull(await context.CarpoolOffers.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == seeded.FreshOfferId, CancellationToken.None));

        Assert.NotNull(await context.CarpoolRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == seeded.FreshRequestId, CancellationToken.None));
    }

    [Fact]
    public async Task Gallring_RaknarVadSomTogsBort()
    {
        var seeded = await SeedAsync("rakning");

        var result = await PurgeAsync();

        Assert.True(result.Requests >= 1);
        Assert.True(result.Offers >= 1);
        Assert.True(result.RemovedAnything);
        Assert.NotEqual(Guid.Empty, seeded.OldOfferId);
    }

    [Fact]
    public async Task Gallring_GarAttKoraTvaGanger()
    {
        /*
         * Jobbet kor vid varje uppstart, och Render free vaknar flera ganger om dagen. Ett
         * andra varv far darfor inte kasta -- det ska bara inte hitta nagot.
         */
        await SeedAsync("upprepning");

        await PurgeAsync();
        var second = await PurgeAsync();

        Assert.False(second.RemovedAnything);
    }
}
