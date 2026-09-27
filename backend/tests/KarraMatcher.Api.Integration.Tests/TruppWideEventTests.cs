using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// En trupp-vid händelse (utan lag) hör till hela truppen (`#332` 1b). Medlemskapet ska då
/// avgöras på trupp-nivå: vem som helst i truppen är medlem, inte bara ett visst lag. Det här
/// vaktar just den vägen i <see cref="IMembershipService"/> — den som öppnar en trupp-vid
/// händelse när admin kan skapa sådana (1b-ii).
/// </summary>
public sealed class TruppWideEventTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Seeded(Guid EventId, Guid GuardianId, Guid StrangerId);

    private async Task<Seeded> SeedTruppWideEventAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-tw-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-tw-{suffix}",
        };

        // Kärnan: en händelse som hör till truppen men inte till något lag.
        var truppWide = new Event
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = null,
            Type = EventType.Training,
            Title = "Trupp-träning",
            KickoffUtc = now.AddDays(3),
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };

        var guardian = new Account { Id = Guid.NewGuid(), Email = $"vh-tw-{suffix}@example.com", CreatedUtc = now };
        var stranger = new Account { Id = Guid.NewGuid(), Email = $"frammling-tw-{suffix}@example.com", CreatedUtc = now };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = trupp.Id,
            TeamId = team.Id,
            CreatedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Events.Add(truppWide);
        context.Accounts.AddRange(guardian, stranger);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        return new Seeded(truppWide.Id, guardian.Id, stranger.Id);
    }

    [Fact]
    public async Task Vardnadshavare_ITruppen_ArMedlem_IEnTruppVidHandelse()
    {
        var seeded = await SeedTruppWideEventAsync("member");

        using var scope = factory.Services.CreateScope();
        var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();

        Assert.True(
            await membership.IsMemberOfEventAsync(seeded.GuardianId, seeded.EventId, CancellationToken.None),
            "En vårdnadshavare i truppen ska vara medlem i en trupp-vid händelse.");
    }

    [Fact]
    public async Task Frammling_ArInteMedlem_IEnTruppVidHandelse()
    {
        var seeded = await SeedTruppWideEventAsync("stranger");

        using var scope = factory.Services.CreateScope();
        var membership = scope.ServiceProvider.GetRequiredService<IMembershipService>();

        Assert.False(
            await membership.IsMemberOfEventAsync(seeded.StrangerId, seeded.EventId, CancellationToken.None),
            "Någon utanför truppen ska inte vara medlem i en trupp-vid händelse.");
    }
}
