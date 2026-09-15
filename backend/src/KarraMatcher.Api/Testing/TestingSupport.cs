using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Matches;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using DomainMatch = KarraMatcher.Domain.Matches.Match;

namespace KarraMatcher.Api.Testing;

/// <summary>
/// Stöd som <b>bara</b> finns när <c>Testing:Enabled</c> är satt — för E2E-testerna (`#71`).
///
/// <para>
/// <b>Aldrig i drift.</b> Uppstarten faller om flaggan är på i Production (se <c>Program.cs</c>),
/// och endpointsen registreras som minimala API:er först när flaggan är på, så att de inte ens
/// existerar för gästvakten eller i de vanliga integrationstesterna. Ytan är avsiktligt liten:
/// en brevlåda som fångar inloggningskoden (som annars bara hashad når databasen) och en
/// förberedelse-endpoint som skapar de konton, roller och den match E2E-flödena behöver.
/// </para>
/// </summary>
public static class TestingSupport
{
    public const string EnabledKey = "Testing:Enabled";

    private const string CoachEmail = "coach-e2e@test.local";
    private const string ParentAEmail = "parent-a-e2e@test.local";
    private const string ParentBEmail = "parent-b-e2e@test.local";
    private const string E2eTeamSlug = "gul";
    private const string E2eOpponent = "E2E FC";

    public static IServiceCollection AddTestingSupport(this IServiceCollection services)
    {
        services.AddSingleton<TestMailbox>();

        // Ersätter e-postsändaren som infrastrukturen registrerat — den sist registrerade
        // vinner vid upplösning av en enda IEmailSender. Fångar koden i stället för att skicka.
        services.AddScoped<IEmailSender, TestMailboxEmailSender>();

        return services;
    }

    public static void MapTestingEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/v1/testing");

        // Skapar allt E2E-flödena behöver, idempotent. Kontona har förnamn satt så att
        // inloggningen inte stannar på namnformuläret.
        group.MapPost("/prepare", async (KarraMatcherDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var coach = await EnsureAccountAsync(db, CoachEmail, clock, ct).ConfigureAwait(false);
            await EnsureAccountAsync(db, ParentAEmail, clock, ct).ConfigureAwait(false);
            await EnsureAccountAsync(db, ParentBEmail, clock, ct).ConfigureAwait(false);

            var team = await db.Teams.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == E2eTeamSlug, ct).ConfigureAwait(false);

            if (team is null)
            {
                return Results.Problem("Seed saknas: laget 'gul' finns inte. Kör med SeedOnStartup=true.");
            }

            await EnsureCoachRoleAsync(db, coach.Id, team.Id, clock, ct).ConfigureAwait(false);

            var matchId = await EnsureFutureMatchAsync(db, team.Id, clock, ct).ConfigureAwait(false);

            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            return Results.Ok(new PrepareResult(
                CoachEmail, ParentAEmail, ParentBEmail, E2eTeamSlug, matchId));
        });

        // Nollställer samåkningen, så ett samåkningsflöde alltid startar med tom lista och
        // därför exakt ett erbjudande att interagera med. ExecuteDelete kräver en riktig
        // databas (Postgres i E2E), vilket testläget alltid har.
        group.MapPost("/reset-carpool", async (KarraMatcherDbContext db, CancellationToken ct) =>
        {
            await db.CarpoolRequests.ExecuteDeleteAsync(ct).ConfigureAwait(false);
            await db.CarpoolOffers.ExecuteDeleteAsync(ct).ConfigureAwait(false);
            return Results.NoContent();
        });

        // Den senaste inloggningskoden för en adress, eller 404 om ingen skickats än.
        group.MapGet("/code", (string email, TestMailbox mailbox) =>
        {
            var code = mailbox.Latest(email);
            return code is null ? Results.NotFound() : Results.Ok(new CodeResult(code));
        });
    }

    private static async Task<Account> EnsureAccountAsync(
        KarraMatcherDbContext db,
        string email,
        TimeProvider clock,
        CancellationToken ct)
    {
        var normalized = email.ToLowerInvariant();

        var existing = await db.Accounts
            .FirstOrDefaultAsync(a => a.Email == normalized, ct).ConfigureAwait(false);

        if (existing is not null)
        {
            return existing;
        }

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = normalized,
            FirstName = "E2E",
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        };

        await db.Accounts.AddAsync(account, ct).ConfigureAwait(false);

        return account;
    }

    private static async Task EnsureCoachRoleAsync(
        KarraMatcherDbContext db,
        Guid accountId,
        Guid teamId,
        TimeProvider clock,
        CancellationToken ct)
    {
        var exists = await db.TeamRoles.AnyAsync(
            r => r.AccountId == accountId && r.TeamId == teamId && r.Role == RoleKind.Coach,
            ct).ConfigureAwait(false);

        if (exists)
        {
            return;
        }

        await db.TeamRoles.AddAsync(
            new TeamRole
            {
                Id = Guid.NewGuid(),
                AccountId = accountId,
                TeamId = teamId,
                Role = RoleKind.Coach,
                GrantedUtc = clock.GetUtcNow().UtcDateTime,
            },
            ct).ConfigureAwait(false);
    }

    private static async Task<Guid> EnsureFutureMatchAsync(
        KarraMatcherDbContext db,
        Guid teamId,
        TimeProvider clock,
        CancellationToken ct)
    {
        var existing = await db.Matches.AsNoTracking()
            .Where(m => m.TeamId == teamId && m.OpponentName == E2eOpponent)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (existing is not null)
        {
            return existing.Value;
        }

        // En hemmavenue finns i seeden (Klarebergsvallen). Vilken som helst duger för testet.
        var venueId = await db.Venues.AsNoTracking()
            .Select(v => v.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        var kickoff = clock.GetUtcNow().UtcDateTime.AddDays(7);

        var match = new DomainMatch
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            KickoffUtc = kickoff,
            OpponentName = E2eOpponent,
            VenueId = venueId,
            IsHome = true,
            Status = MatchStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = kickoff,
        };

        await db.Matches.AddAsync(match, ct).ConfigureAwait(false);

        return match.Id;
    }

    private sealed record PrepareResult(
        string CoachEmail,
        string ParentAEmail,
        string ParentBEmail,
        string TeamSlug,
        Guid MatchId);

    private sealed record CodeResult(string Code);
}

/// <summary>Fångar den senaste inloggningskoden per adress. Bara i testläge.</summary>
public sealed class TestMailbox
{
    private readonly ConcurrentDictionary<string, string> _codes = new(StringComparer.OrdinalIgnoreCase);

    public void Store(string email, string code) => _codes[email] = code;

    public string? Latest(string email) => _codes.TryGetValue(email, out var code) ? code : null;
}

/// <summary>
/// En e-postsändare som inte skickar något utan fångar koden ur mejltexten (`Din kod är 123456`)
/// och lägger den i <see cref="TestMailbox"/>. Registreras bara i testläge.
/// </summary>
public sealed partial class TestMailboxEmailSender(TestMailbox mailbox) : IEmailSender
{
    public Task SendAsync(string recipient, string subject, string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var match = CodePattern().Match(body);

        if (match.Success)
        {
            mailbox.Store(recipient, match.Value);
        }

        return Task.CompletedTask;
    }

    [GeneratedRegex(@"\d{6}")]
    private static partial Regex CodePattern();
}
