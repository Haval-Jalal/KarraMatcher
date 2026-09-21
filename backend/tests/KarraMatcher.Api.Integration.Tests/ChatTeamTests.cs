using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Chat;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Chat;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Lag-chatten (`#202`): egna kanaler per färg-lag ovanpå trupp-chatten. Bara lagets
/// medlemmar; kanalerna är isolerade; moderering och gallring delas med trupp-chatten.
/// </summary>
public sealed class ChatTeamTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private (WebApplicationFactory<Program> App, RecordingOutbox Outbox) WithRecordingOutbox()
    {
        var outbox = new RecordingOutbox();
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushOutbox>();
            services.AddSingleton<IPushOutbox>(outbox);
        }));

        return (app, outbox);
    }

    private sealed record Fixture(
        Guid TruppId,
        Guid GulTeamId,
        string GulSlug,
        Guid BlaTeamId,
        string BlaSlug,
        Guid AdminId,
        Guid CoachId,
        Guid GulGuardianId,
        Guid BlaGuardianId,
        Guid NonMemberId);

    // ---- Kanal-isolering -------------------------------------------------------------

    [Fact]
    public async Task TeamKanal_ArIsoleradFranTruppKanalen()
    {
        var f = await SeedAsync("iso");

        await TeamPostAsync(f, f.GulSlug, GulGuardianToken(f), "Bara för Gul");
        await TruppPostAsync(f, AdminToken(f), "Till hela truppen");

        var gul = await TeamArrayAsync(f.GulSlug, "messages", GulGuardianToken(f));
        Assert.Contains(gul, m => m.GetProperty("body").GetString() == "Bara för Gul");
        Assert.DoesNotContain(gul, m => m.GetProperty("body").GetString() == "Till hela truppen");

        var trupp = await TruppArrayAsync(f, "messages", AdminToken(f));
        Assert.Contains(trupp, m => m.GetProperty("body").GetString() == "Till hela truppen");
        Assert.DoesNotContain(trupp, m => m.GetProperty("body").GetString() == "Bara för Gul");
    }

    // ---- Behörighet per lag ----------------------------------------------------------

    [Fact]
    public async Task AnnatLagsVardnadshavare_NekasIKanalen()
    {
        var f = await SeedAsync("behorighet");

        var blaInGul = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{f.GulSlug}/chat/messages", BlaGuardianToken(f));
        Assert.Equal(HttpStatusCode.Forbidden, blaInGul.StatusCode);

        var gulInGul = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{f.GulSlug}/chat/messages", GulGuardianToken(f));
        Assert.Equal(HttpStatusCode.OK, gulInGul.StatusCode);

        var gulInBla = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{f.BlaSlug}/chat/messages", GulGuardianToken(f));
        Assert.Equal(HttpStatusCode.Forbidden, gulInBla.StatusCode);
    }

    [Fact]
    public async Task Admin_NarAllaLagKanaler()
    {
        var f = await SeedAsync("admin-alla");

        var gul = await SendAsync(
            HttpMethod.Post, $"/api/v1/teams/{f.GulSlug}/chat/messages", AdminToken(f),
            new { body = "Admin i Gul" });
        Assert.Equal(HttpStatusCode.NoContent, gul.StatusCode);

        var bla = await SendAsync(
            HttpMethod.Post, $"/api/v1/teams/{f.BlaSlug}/chat/messages", AdminToken(f),
            new { body = "Admin i Blå" });
        Assert.Equal(HttpStatusCode.NoContent, bla.StatusCode);
    }

    // ---- Meta ------------------------------------------------------------------------

    [Fact]
    public async Task Meta_GerTruppIdOchLedarskap()
    {
        var f = await SeedAsync("meta");

        Assert.True(await MetaLeaderAsync(f, f.GulSlug, AdminToken(f), f.TruppId));
        Assert.True(await MetaLeaderAsync(f, f.GulSlug, CoachToken(f), f.TruppId));
        Assert.False(await MetaLeaderAsync(f, f.GulSlug, GulGuardianToken(f), f.TruppId));
    }

    // ---- Schemaläggning i lag-kanalen ------------------------------------------------

    [Fact]
    public async Task Schemalagg_ILagKanal_BaraLedare()
    {
        var f = await SeedAsync("schema");

        var denied = await SendAsync(
            HttpMethod.Post, $"/api/v1/teams/{f.GulSlug}/chat/messages", GulGuardianToken(f),
            new { body = "I morgon", publishAt = DateTimeOffset.UtcNow.AddDays(1) });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var scheduled = await SendAsync(
            HttpMethod.Post, $"/api/v1/teams/{f.GulSlug}/chat/messages", CoachToken(f),
            new { body = "Match på lördag", publishAt = DateTimeOffset.UtcNow.AddDays(1) });
        Assert.Equal(HttpStatusCode.NoContent, scheduled.StatusCode);

        Assert.Empty(await TeamArrayAsync(f.GulSlug, "messages", CoachToken(f)));
        var mine = await TeamArrayAsync(f.GulSlug, "scheduled", CoachToken(f));
        Assert.Single(mine);
        Assert.Equal("Match på lördag", mine[0].GetProperty("body").GetString());
    }

    // ---- Moderering ------------------------------------------------------------------

    [Fact]
    public async Task Radera_ILagKanal_EgetEllerAdmin()
    {
        var f = await SeedAsync("radera");
        var id = await TeamPostAsync(f, f.GulSlug, CoachToken(f), "Coachens rad");

        var denied = await SendAsync(
            HttpMethod.Delete, $"/api/v1/teams/{f.GulSlug}/chat/messages/{id}", GulGuardianToken(f));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var allowed = await SendAsync(
            HttpMethod.Delete, $"/api/v1/teams/{f.GulSlug}/chat/messages/{id}", AdminToken(f));
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);

        var message = (await TeamArrayAsync(f.GulSlug, "messages", CoachToken(f)))
            .Single(m => m.GetProperty("id").GetGuid() == id);
        Assert.True(message.GetProperty("deleted").GetBoolean());
        Assert.Equal(string.Empty, message.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Anmalan_ILagKanal_SynsITruppensAdminKo()
    {
        var f = await SeedAsync("anmalan");
        var id = await TeamPostAsync(f, f.GulSlug, CoachToken(f), "Något olämpligt");

        var report = await SendAsync(
            HttpMethod.Post, $"/api/v1/teams/{f.GulSlug}/chat/messages/{id}/report", GulGuardianToken(f));
        Assert.Equal(HttpStatusCode.NoContent, report.StatusCode);
        await AssertAuditedAsync("chatt.meddelande.anmalt", id);

        // Truppens admin-kö spänner över lag-kanalerna (delad moderering, `#202`).
        var reports = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{f.TruppId}/chat/reports", AdminToken(f));
        reports.EnsureSuccessStatusCode();
        var body = await reports.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var row = body.EnumerateArray().Single(r => r.GetProperty("messageId").GetGuid() == id);
        Assert.Equal(1, row.GetProperty("reportCount").GetInt32());
    }

    [Fact]
    public async Task AdminRaderar_LagMeddelande_FranTruppensKo()
    {
        var f = await SeedAsync("admin-radera-lag");
        var id = await TeamPostAsync(f, f.GulSlug, CoachToken(f), "Ska modereras bort");

        // Admin raderar ett lag-kanal-meddelande via den kanalobundna trupp-admin-endpointen.
        var deleted = await SendAsync(
            HttpMethod.Delete, $"/api/v1/admin/trupper/{f.TruppId}/chat/messages/{id}", AdminToken(f));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await AssertAuditedAsync("chatt.meddelande.raderat", id);

        // Meddelandet är nu en tombstone i lag-kanalen (texten borta).
        var messages = await SendAsync(
            HttpMethod.Get, $"/api/v1/teams/{f.GulSlug}/chat/messages", AdminToken(f));
        messages.EnsureSuccessStatusCode();
        var list = await messages.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var msg = list.EnumerateArray().Single(m => m.GetProperty("id").GetGuid() == id);
        Assert.True(msg.GetProperty("deleted").GetBoolean());
        Assert.Equal(string.Empty, msg.GetProperty("body").GetString());
    }

    // ---- Notis -----------------------------------------------------------------------

    [Fact]
    public async Task Notis_TillLagetsMedlemmar_InteAnnatLag()
    {
        var f = await SeedAsync("notis");
        var (app, outbox) = WithRecordingOutbox();

        // Coachen (Gul) postar i Gul-kanalen.
        var post = await SendVia(app, HttpMethod.Post, $"/api/v1/teams/{f.GulSlug}/chat/messages",
            CoachToken(f), new { body = "Hej Gul" });
        Assert.Equal(HttpStatusCode.NoContent, post.StatusCode);

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal(PushCategory.Chat, dispatch.Category);
        Assert.NotNull(dispatch.AccountIds);
        Assert.Contains(f.AdminId, dispatch.AccountIds!); // medlem av alla lag
        Assert.Contains(f.GulGuardianId, dispatch.AccountIds!);
        Assert.DoesNotContain(f.CoachId, dispatch.AccountIds!); // författaren
        Assert.DoesNotContain(f.BlaGuardianId, dispatch.AccountIds!); // inte medlem av Gul
    }

    // ---- Gallring (delas med trupp-chatten) ------------------------------------------

    [Fact]
    public async Task Gallring_TarLagKanalMeddelande()
    {
        var f = await SeedAsync("gallring");
        var now = DateTime.UtcNow;
        var oldId = Guid.NewGuid();

        await WithScopeAsync(async context =>
        {
            context.ChatMessages.Add(new ChatMessage
            {
                Id = oldId,
                AgeGroupId = f.TruppId,
                TeamId = f.GulTeamId,
                AuthorAccountId = f.CoachId,
                Body = "Gammalt i Gul",
                CreatedUtc = now.AddDays(-91),
                PublishAtUtc = now.AddDays(-91),
                PublishedUtc = now.AddDays(-91),
            });
            await context.SaveChangesAsync(CancellationToken.None);
        });

        using (var scope = factory.Services.CreateScope())
        {
            var retention = scope.ServiceProvider.GetRequiredService<ChatRetentionService>();
            await retention.PurgeAsync(CancellationToken.None);
        }

        await WithScopeAsync(async context =>
            Assert.Null(await context.ChatMessages.AsNoTracking()
                .SingleOrDefaultAsync(m => m.Id == oldId, CancellationToken.None)));
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private async Task<Fixture> SeedAsync(string suffix)
    {
        var now = DateTime.UtcNow;
        var truppId = Guid.NewGuid();
        var gulTeamId = Guid.NewGuid();
        var blaTeamId = Guid.NewGuid();
        var gulSlug = $"gul-teamchat-{suffix}";
        var blaSlug = $"bla-teamchat-{suffix}";
        var adminId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var gulGuardianId = Guid.NewGuid();
        var blaGuardianId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();

        await WithScopeAsync(async context =>
        {
            var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-teamchat-{suffix}" };
            context.Clubs.Add(club);
            context.AgeGroups.Add(new AgeGroup
            {
                Id = truppId,
                ClubId = club.Id,
                Name = "P2016",
                Season = "2026",
            });
            context.Teams.AddRange(
                new Team
                {
                    Id = gulTeamId,
                    AgeGroupId = truppId,
                    Name = "Gul",
                    ColorHex = "#D9A21B",
                    Slug = gulSlug,
                },
                new Team
                {
                    Id = blaTeamId,
                    AgeGroupId = truppId,
                    Name = "Blå",
                    ColorHex = "#1B4F9B",
                    Slug = blaSlug,
                });

            context.Accounts.AddRange(
                Account(adminId, "admin", suffix, "Admin"),
                Account(coachId, "coach", suffix, "Coach"),
                Account(gulGuardianId, "gul", suffix, "GulVH"),
                Account(blaGuardianId, "bla", suffix, "BlaVH"),
                Account(nonMemberId, "utom", suffix, "Utom"));

            context.TeamRoles.AddRange(
                new TeamRole
                {
                    Id = Guid.NewGuid(),
                    AccountId = adminId,
                    AgeGroupId = truppId,
                    Role = RoleKind.Admin,
                    GrantedUtc = now,
                },
                new TeamRole
                {
                    Id = Guid.NewGuid(),
                    AccountId = coachId,
                    TeamId = gulTeamId,
                    Role = RoleKind.Coach,
                    GrantedUtc = now,
                });

            AddGuardianChild(context, truppId, gulTeamId, gulGuardianId, now, "Liam");
            AddGuardianChild(context, truppId, blaTeamId, blaGuardianId, now, "Nova");

            await context.SaveChangesAsync(CancellationToken.None);
        });

        return new Fixture(
            truppId, gulTeamId, gulSlug, blaTeamId, blaSlug,
            adminId, coachId, gulGuardianId, blaGuardianId, nonMemberId);
    }

    private static Account Account(Guid id, string tag, string suffix, string firstName) =>
        new()
        {
            Id = id,
            Email = $"{tag}-{suffix}@example.com",
            FirstName = firstName,
            CreatedUtc = DateTime.UtcNow,
        };

    private static void AddGuardianChild(
        KarraMatcherDbContext context, Guid truppId, Guid teamId, Guid accountId, DateTime now, string first)
    {
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = first,
            LastInitial = "X",
            AgeGroupId = truppId,
            TeamId = teamId,
            CreatedUtc = now,
        };
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ChildId = child.Id,
            GrantedUtc = now,
        });
    }

    private async Task WithScopeAsync(Func<KarraMatcherDbContext, Task> work)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        await work(context);
    }

    private async Task<Guid> TeamPostAsync(Fixture f, string slug, string token, string body)
    {
        var post = await SendAsync(
            HttpMethod.Post, $"/api/v1/teams/{slug}/chat/messages", token, new { body });
        post.EnsureSuccessStatusCode();

        var messages = await TeamArrayAsync(slug, "messages", token);
        return messages.Last(m => m.GetProperty("body").GetString() == body).GetProperty("id").GetGuid();
    }

    private async Task TruppPostAsync(Fixture f, string token, string body)
    {
        var post = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages", token, new { body });
        post.EnsureSuccessStatusCode();
    }

    private async Task<bool> MetaLeaderAsync(Fixture f, string slug, string token, Guid expectedTruppId)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/teams/{slug}/chat/meta", token);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(expectedTruppId, body.GetProperty("truppId").GetGuid());
        return body.GetProperty("isLeader").GetBoolean();
    }

    private async Task<IReadOnlyList<JsonElement>> TeamArrayAsync(string slug, string path, string token)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/teams/{slug}/chat/{path}", token);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return [.. body.EnumerateArray().Select(e => e.Clone())];
    }

    private async Task<IReadOnlyList<JsonElement>> TruppArrayAsync(Fixture f, string path, string token)
    {
        var response = await SendAsync(
            HttpMethod.Get, $"/api/v1/trupper/{f.TruppId}/chat/{path}", token);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        return [.. body.EnumerateArray().Select(e => e.Clone())];
    }

    private string AdminToken(Fixture f) =>
        Token(f.AdminId, new AccountRoles(false, [f.TruppId.ToString()], []), "admin@test");

    private string CoachToken(Fixture f) =>
        Token(f.CoachId, new AccountRoles(false, [], [f.GulSlug]), "coach@test");

    private string GulGuardianToken(Fixture f) => Token(f.GulGuardianId, AccountRoles.None, "gul@test");

    private string BlaGuardianToken(Fixture f) => Token(f.BlaGuardianId, AccountRoles.None, "bla@test");

    private string Token(Guid accountId, AccountRoles roles, string email)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer.Issue(accountId, email, roles).Token;
    }

    private async Task AssertAuditedAsync(string action, Guid subjectId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var found = await context.AuditEntries.AsNoTracking()
            .AnyAsync(e => e.Action == action && e.SubjectId == subjectId, CancellationToken.None);

        Assert.True(found, $"Ingen audit-rad '{action}' för {subjectId}.");
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, string token, object? payload = null) =>
        SendVia(factory, method, path, token, payload);

    private static async Task<HttpResponseMessage> SendVia(
        WebApplicationFactory<Program> app, HttpMethod method, string path, string token, object? payload)
    {
        using var client = app.CreateClient(ClientOptions);
        var (csrf, cookie) = await GetCsrfAsync(client, token);

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);

        if (payload is not null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return await client.SendAsync(request, CancellationToken.None);
    }

    private static async Task<(string Token, string Cookie)> GetCsrfAsync(
        HttpClient client, string accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        return (body.GetProperty("token").GetString()!, cookie);
    }
}
