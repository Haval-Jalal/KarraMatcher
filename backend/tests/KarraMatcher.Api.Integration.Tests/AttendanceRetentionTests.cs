using KarraMatcher.Application.Features.Attendance;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Gallringen av kallelser (§KM.7/§KM.10, `#203`). Kallelser för händelser äldre än 30 dagar
/// tas bort med sina per-barn-svar; nyare lämnas kvar.
/// </summary>
public sealed class AttendanceRetentionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Seeded(Guid OldCallId, Guid OldInvitationId, Guid FreshCallId);

    private async Task<Seeded> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-kg-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-kg-{suffix}",
        };
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = $"Plan {suffix}",
            Address = "Klarebergsvallen",
            Latitude = 57.8,
            Longitude = 12,
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
        var coach = new Account { Id = Guid.NewGuid(), Email = $"c-kg-{suffix}@example.com", CreatedUtc = now };

        // 31 dagar sedan händelsen: bortom gränsen; 29 dagar: strax innanför.
        var oldEvent = Event(team.Id, venue.Id, now.AddDays(-31));
        var freshEvent = Event(team.Id, venue.Id, now.AddDays(-29));

        var oldCall = Call(oldEvent.Id, coach.Id, now.AddDays(-32));
        var freshCall = Call(freshEvent.Id, coach.Id, now.AddDays(-30));
        var oldInvitation = new AttendanceInvitation
        {
            Id = Guid.NewGuid(),
            CallId = oldCall.Id,
            ChildId = child.Id,
            Reply = AttendanceReply.Coming,
            RespondedByAccountId = coach.Id,
            RespondedUtc = now.AddDays(-31),
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Children.Add(child);
        context.Accounts.Add(coach);
        context.Events.AddRange(oldEvent, freshEvent);
        context.AttendanceCalls.AddRange(oldCall, freshCall);
        context.AttendanceInvitations.Add(oldInvitation);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Seeded(oldCall.Id, oldInvitation.Id, freshCall.Id);
    }

    private static Event Event(Guid teamId, Guid venueId, DateTime kickoffUtc) => new()
    {
        Id = Guid.NewGuid(),
        TeamId = teamId,
        Type = EventType.Match,
        KickoffUtc = kickoffUtc,
        OpponentName = "Motståndare",
        IsHome = true,
        VenueId = venueId,
        Status = EventStatus.Scheduled,
        UpdatedUtc = kickoffUtc,
    };

    private static AttendanceCall Call(Guid eventId, Guid openedBy, DateTime openedUtc) => new()
    {
        Id = Guid.NewGuid(),
        MatchId = eventId,
        OpenedByAccountId = openedBy,
        OpenedUtc = openedUtc,
    };

    private async Task<int> PurgeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<AttendanceRetentionService>();

        return await retention.PurgeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Gallring_TarBortGamlaKallelserOchSvar()
    {
        var seeded = await SeedAsync("bort");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Null(await context.AttendanceCalls.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == seeded.OldCallId, CancellationToken.None));
        Assert.Null(await context.AttendanceInvitations.AsNoTracking()
            .SingleOrDefaultAsync(i => i.Id == seeded.OldInvitationId, CancellationToken.None));
    }

    [Fact]
    public async Task Gallring_LamnarKvarDetSomInteNattGransen()
    {
        var seeded = await SeedAsync("kvar");

        await PurgeAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.NotNull(await context.AttendanceCalls.AsNoTracking()
            .SingleOrDefaultAsync(c => c.Id == seeded.FreshCallId, CancellationToken.None));
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
