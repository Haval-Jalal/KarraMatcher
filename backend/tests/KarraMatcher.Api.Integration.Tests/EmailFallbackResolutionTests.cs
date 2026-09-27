using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Push;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Vem e-postfallbacken når: lagets medlemmar som vill ha kategorin men saknar en fungerande
/// push-prenumeration (`#200`).
///
/// <para>
/// Komplementet till push: har man en enhet får man push och inget mejl; har man stängt av
/// kategorin nås man inte alls; annars mejlet. En vuxen har alltid en adress.
/// </para>
/// </summary>
public sealed class EmailFallbackResolutionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    [Fact]
    public async Task NarBaraMedlemmar_UtanPush_SomInteStangtAvKategorin()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-e-{Guid.NewGuid():N}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-e-{Guid.NewGuid():N}",
            AttendanceEnabled = true,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);

        // A: har en push-enhet → nås av push, inget mejl.
        var a = SeedMember(context, team, "a");
        context.PushSubscriptions.Add(new PushSubscription
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            AccountId = a.Id,
            Endpoint = "https://push.example/a",
            P256dh = "key",
            Auth = "auth",
            CreatedUtc = now,
        });

        // B: ingen enhet (notiser av = avregistrerad enhet, `#332`-uppföljning) → nås inte av
        // push → mejlas, så man missar inget kritiskt.
        var b = SeedMember(context, team, "b");

        await context.SaveChangesAsync(CancellationToken.None);

        var repository = scope.ServiceProvider.GetRequiredService<IEmailFallbackRepository>();

        // Mejl-säkerhetsnätet når alla som inte nås av push: B (ingen enhet) — men inte A (enhet).
        // Det finns ingen separat global flagga längre; en avslagen enhet har ingen prenumeration.
        var recipients = await repository.ListForTeamAsync(
            team.Id, PushCategory.Kallelse, CancellationToken.None);

        Assert.Equal(
            [b.Email],
            recipients.Select(r => r.Email).OrderBy(e => e, StringComparer.Ordinal).ToArray());
    }

    private static Account SeedMember(KarraMatcherDbContext context, Team team, string tag)
    {
        var now = DateTime.UtcNow;

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"vh-{tag}-{Guid.NewGuid():N}@example.com",
            CreatedUtc = now,
        };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = team.AgeGroupId,
            TeamId = team.Id,
            CreatedUtc = now,
        };

        context.Accounts.Add(account);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = account.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });

        return account;
    }
}
