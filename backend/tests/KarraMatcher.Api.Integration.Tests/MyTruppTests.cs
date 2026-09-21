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
/// "Mina trupper" (`#201`): en medlem hittar sina trupper (för t.ex. chatten) utan att vara
/// admin, och ser om hen är ledare. En icke-medlem får en tom lista.
/// </summary>
public sealed class MyTruppTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Fixture(Guid TruppId, Guid AdminId, Guid GuardianId, Guid NonMemberId);

    [Fact]
    public async Task Admin_SerSinTrupp_SomLedare()
    {
        var f = await SeedAsync("admin");

        var trupper = await MinaAsync(f.AdminId);

        var trupp = trupper.Single();
        Assert.Equal(f.TruppId, trupp.GetProperty("id").GetGuid());
        Assert.True(trupp.GetProperty("isLeader").GetBoolean());
    }

    [Fact]
    public async Task Vardnadshavare_SerSinTrupp_InteSomLedare()
    {
        var f = await SeedAsync("vh");

        var trupper = await MinaAsync(f.GuardianId);

        var trupp = trupper.Single();
        Assert.Equal(f.TruppId, trupp.GetProperty("id").GetGuid());
        Assert.False(trupp.GetProperty("isLeader").GetBoolean());
    }

    [Fact]
    public async Task IckeMedlem_FarTomLista()
    {
        var f = await SeedAsync("utom");

        Assert.Empty(await MinaAsync(f.NonMemberId));
    }

    private async Task<IReadOnlyList<JsonElement>> MinaAsync(Guid accountId)
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", Token(accountId));

        var response = await client.GetAsync(new Uri("/api/v1/trupper/mina", UriKind.Relative), CancellationToken.None);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return [.. body.EnumerateArray().Select(e => e.Clone())];
    }

    private string Token(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer.Issue(accountId, "konto@test", AccountRoles.None).Token;
    }

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var truppId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();

        var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-mina-{suffix}" };
        context.Clubs.Add(club);
        context.AgeGroups.Add(new AgeGroup { Id = truppId, ClubId = club.Id, Name = "P2016", Season = "2026" });
        context.Teams.Add(new Team
        {
            Id = teamId,
            AgeGroupId = truppId,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-mina-{suffix}",
        });
        context.Accounts.AddRange(
            new Account { Id = adminId, Email = $"admin-mina-{suffix}@example.com", CreatedUtc = now },
            new Account { Id = guardianId, Email = $"vh-mina-{suffix}@example.com", CreatedUtc = now },
            new Account { Id = nonMemberId, Email = $"utom-mina-{suffix}@example.com", CreatedUtc = now });

        context.TeamRoles.Add(new TeamRole
        {
            Id = Guid.NewGuid(),
            AccountId = adminId,
            AgeGroupId = truppId,
            Role = RoleKind.Admin,
            GrantedUtc = now,
        });

        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "X",
            AgeGroupId = truppId,
            TeamId = teamId,
            CreatedUtc = now,
        };
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardianId,
            ChildId = child.Id,
            GrantedUtc = now,
        });

        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(truppId, adminId, guardianId, nonMemberId);
    }
}
