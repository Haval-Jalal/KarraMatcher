using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Truppens aktivitetslista (`#334`): alla händelser i truppen — matcher, träningar, cuper och
/// övrigt, tvärs över lagen och inklusive trupp-vida — för en medlem. En icke-medlem nekas (§KM.3).
/// </summary>
public sealed class TruppActivitiesTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Fixture(Guid TruppId, Guid GuardianId, Guid NonMemberId);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-act-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var gul = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-act-{suffix}",
        };

        // Tre aktiviteter i osorterad ordning — listan ska sortera dem på avspark.
        var match = new Event
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = gul.Id,
            Type = EventType.Match,
            KickoffUtc = now.AddDays(2),
            OpponentName = "Torslanda",
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };
        var training = new Event
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = null, // trupp-vid
            Type = EventType.Training,
            KickoffUtc = now.AddDays(1),
            Title = "Gemensam träning",
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };
        var cup = new Event
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = gul.Id,
            Type = EventType.Cup,
            KickoffUtc = now.AddDays(3),
            Title = "Sommarcup",
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };

        var guardian = new Account { Id = Guid.NewGuid(), Email = $"vh-act-{suffix}@example.com", CreatedUtc = now };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = trupp.Id,
            TeamId = gul.Id,
            CreatedUtc = now,
        };
        var nonMember = new Account { Id = Guid.NewGuid(), Email = $"utom-act-{suffix}@example.com", CreatedUtc = now };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(gul);
        context.Events.AddRange(match, training, cup);
        context.Accounts.AddRange(guardian, nonMember);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });

        // Matcher grindas av kallelse (`#514`): barnet kallas till matchen så vårdnadshavaren ser
        // den i listan. Träning/cup syns ändå för alla medlemmar.
        AttendanceSeed.CallChildrenToMatch(context, match.Id, guardian.Id, child.Id);

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(trupp.Id, guardian.Id, nonMember.Id);
    }

    private string Token(Guid accountId) =>
        TokenFor(accountId, AccountRoles.None);

    private string TokenFor(Guid accountId, AccountRoles roles)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer.Issue(accountId, "konto@example.com", roles).Token;
    }

    private async Task<HttpResponseMessage> GetAsync(string path, string token)
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Medlem_SerAllaTruppensAktiviteter_IAvsparksordning()
    {
        var f = await SeedAsync("member");

        var response = await GetAsync($"/api/v1/trupper/{f.TruppId}/events", Token(f.GuardianId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None))
            .EnumerateArray()
            .ToList();

        Assert.Equal(3, items.Count);

        // Sorterade på avspark: träning (dag 1), match (dag 2), cup (dag 3).
        Assert.Equal("Training", items[0].GetProperty("event").GetProperty("type").GetString());
        Assert.Equal("Match", items[1].GetProperty("event").GetProperty("type").GetString());
        Assert.Equal("Cup", items[2].GetProperty("event").GetProperty("type").GetString());

        // Trupp-vid händelse: inget lag (ingen lagfärg).
        Assert.Equal(JsonValueKind.Null, items[0].GetProperty("team").ValueKind);

        // Lag-riktad match: lagets namn och färg följer med.
        Assert.Equal("Gul", items[1].GetProperty("team").GetProperty("name").GetString());
    }

    [Fact]
    public async Task IckeMedlem_Nekas()
    {
        var f = await SeedAsync("outsider");

        var response = await GetAsync($"/api/v1/trupper/{f.TruppId}/events", Token(f.NonMemberId));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Medlem_UtanKallelse_SerTraningMenIngenMatch()
    {
        // §KM.7, ägarbeslut (`#514`): en match man inte är kallad på ska inte synas — men träningen
        // (fasta tider varje vecka) syns för alla truppens medlemmar.
        var (truppId, guardianId, matchId, trainingId) = await SeedUncalledAsync("okallad");

        var response = await GetAsync($"/api/v1/trupper/{truppId}/events", Token(guardianId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ids = (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None))
            .EnumerateArray()
            .Select(item => item.GetProperty("event").GetProperty("id").GetGuid())
            .ToList();

        Assert.Contains(trainingId, ids);
        Assert.DoesNotContain(matchId, ids);
    }

    [Fact]
    public async Task Medlem_UtanKallelse_KanInteOppnaMatchen()
    {
        // Samma grind på detaljsidan: matchen går inte att öppna utan kallelse (403), men träningen
        // gör det.
        var (_, guardianId, matchId, trainingId) = await SeedUncalledAsync("okallad-detalj");

        var match = await GetAsync($"/api/v1/events/{matchId}", Token(guardianId));
        Assert.Equal(HttpStatusCode.Forbidden, match.StatusCode);

        var training = await GetAsync($"/api/v1/events/{trainingId}", Token(guardianId));
        Assert.Equal(HttpStatusCode.OK, training.StatusCode);
    }

    /// <summary>En trupp med en match och en träning, och en medlem (vårdnadshavare) som INTE är
    /// kallad till matchen.</summary>
    private async Task<(Guid TruppId, Guid GuardianId, Guid MatchId, Guid TrainingId)> SeedUncalledAsync(
        string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-uk-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-uk-{suffix}",
        };
        var match = new Event
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = team.Id,
            Type = EventType.Match,
            KickoffUtc = now.AddDays(2),
            OpponentName = "Torslanda",
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };
        var training = new Event
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = null,
            Type = EventType.Training,
            KickoffUtc = now.AddDays(1),
            Title = "Gemensam träning",
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };
        var guardian = new Account { Id = Guid.NewGuid(), Email = $"vh-uk-{suffix}@example.com", CreatedUtc = now };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Nora",
            LastInitial = "K",
            AgeGroupId = trupp.Id,
            TeamId = team.Id,
            CreatedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Events.AddRange(match, training);
        context.Accounts.Add(guardian);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });

        // Ingen kallelse: barnet är medlem men inte kallat till matchen.
        await context.SaveChangesAsync(CancellationToken.None);

        return (trupp.Id, guardian.Id, match.Id, training.Id);
    }
}
