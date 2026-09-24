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
using KarraMatcher.Domain.Push;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Trupp-chatten (§KM.1/§KM.10, `#201`): medlemmar skriver/läser, icke-medlem nekas, ledare
/// schemalägger, moderering (radera/anmäl) och notis som respekterar Chatt-inställningen.
/// </summary>
public sealed class ChatTests(KarraMatcherApiFactory factory)
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
        Guid TeamId,
        string TeamSlug,
        Guid AdminId,
        Guid CoachId,
        Guid GuardianId,
        Guid Guardian2Id,
        Guid NonMemberId);

    // ---- Läsa/skriva + behörighet ----------------------------------------------------

    [Fact]
    public async Task Medlem_Postar_SynsIListanMedNamn()
    {
        var f = await SeedAsync("post");

        var post = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages",
            GuardianToken(f), new { body = "Vem tar med bollar?" });
        Assert.Equal(HttpStatusCode.NoContent, post.StatusCode);

        var messages = await MessagesAsync(f, GuardianToken(f));
        var mine = messages.Single();
        Assert.Equal("Vem tar med bollar?", mine.GetProperty("body").GetString());
        Assert.Equal("Guardian", mine.GetProperty("authorName").GetString());
        Assert.False(mine.GetProperty("deleted").GetBoolean());
    }

    [Fact]
    public async Task IckeMedlem_Nekas()
    {
        var f = await SeedAsync("icke-medlem");

        var read = await SendAsync(
            HttpMethod.Get, $"/api/v1/trupper/{f.TruppId}/chat/messages", NonMemberToken(f));
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        var write = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages",
            NonMemberToken(f), new { body = "Släpp in mig" });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    // ---- Schemaläggning (bara ledare) ------------------------------------------------

    [Fact]
    public async Task Schemalagg_SomVanligMedlem_Nekas()
    {
        var f = await SeedAsync("schema-medlem");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages",
            GuardianToken(f),
            new { body = "I morgon", publishAt = DateTimeOffset.UtcNow.AddDays(1) });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Schemalagg_SomLedare_SynsBaraBlandSchemalagda()
    {
        var f = await SeedAsync("schema-ledare");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages",
            AdminToken(f),
            new { body = "Kallelse på lördag", publishAt = DateTimeOffset.UtcNow.AddDays(1) });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Inte synligt i chatten än.
        var messages = await MessagesAsync(f, AdminToken(f));
        Assert.Empty(messages);

        // Men bland mina schemalagda.
        var scheduled = await ArrayAsync(f, "scheduled", AdminToken(f));
        Assert.Single(scheduled);
        Assert.Equal("Kallelse på lördag", scheduled[0].GetProperty("body").GetString());
    }

    [Fact]
    public async Task Coach_FarSchemalagga()
    {
        var f = await SeedAsync("schema-coach");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages",
            CoachToken(f),
            new { body = "Träning flyttad", publishAt = DateTimeOffset.UtcNow.AddHours(5) });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AvbokaSchemalagt_TarBortDet()
    {
        var f = await SeedAsync("avboka");

        await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages",
            AdminToken(f), new { body = "Ångrar mig", publishAt = DateTimeOffset.UtcNow.AddDays(1) });

        var scheduled = await ArrayAsync(f, "scheduled", AdminToken(f));
        var id = scheduled.Single().GetProperty("id").GetGuid();

        var cancel = await SendAsync(
            HttpMethod.Delete, $"/api/v1/trupper/{f.TruppId}/chat/scheduled/{id}", AdminToken(f));
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        Assert.Empty(await ArrayAsync(f, "scheduled", AdminToken(f)));
    }

    // ---- Moderering ------------------------------------------------------------------

    [Fact]
    public async Task RaderaEget_TombstoneOchAuditloggas()
    {
        var f = await SeedAsync("radera-eget");
        var id = await PostAsync(f, GuardianToken(f), "Skrev fel");

        var delete = await SendAsync(
            HttpMethod.Delete, $"/api/v1/trupper/{f.TruppId}/chat/messages/{id}", GuardianToken(f));
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var message = (await MessagesAsync(f, GuardianToken(f))).Single();
        Assert.True(message.GetProperty("deleted").GetBoolean());
        Assert.Equal(string.Empty, message.GetProperty("body").GetString());

        await AssertAuditedAsync("chatt.meddelande.raderat", id);
    }

    [Fact]
    public async Task RaderaAnnans_NekasForBadeVanligMedlemOchAdmin()
    {
        // #263: admin raderar inte längre andras meddelanden den vägen — bara ur kön, vid tröskeln.
        var f = await SeedAsync("radera-annans");
        var id = await PostAsync(f, CoachToken(f), "Coachens meddelande");

        var denied = await SendAsync(
            HttpMethod.Delete, $"/api/v1/trupper/{f.TruppId}/chat/messages/{id}", GuardianToken(f));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var adminDenied = await SendAsync(
            HttpMethod.Delete, $"/api/v1/trupper/{f.TruppId}/chat/messages/{id}", AdminToken(f));
        Assert.Equal(HttpStatusCode.Forbidden, adminDenied.StatusCode);
    }

    [Fact]
    public async Task Anmalan_ArIdempotent_BarMotivering_OchSynsForAdmin()
    {
        var f = await SeedAsync("anmalan");
        var id = await PostAsync(f, CoachToken(f), "Något olämpligt");

        var first = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages/{id}/report", GuardianToken(f),
            new { reason = "Stötande språk" });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Samma anmälare igen ändrar varken antal eller den första motiveringen (idempotent).
        var again = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages/{id}/report", GuardianToken(f),
            new { reason = "En annan text" });
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);

        await AssertAuditedAsync("chatt.meddelande.anmalt", id);

        var reports = await SendAsync(
            HttpMethod.Get, $"/api/v1/admin/trupper/{f.TruppId}/chat/reports", AdminToken(f));
        reports.EnsureSuccessStatusCode();
        var body = await reports.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var row = body.EnumerateArray().Single(r => r.GetProperty("messageId").GetGuid() == id);
        Assert.Equal(1, row.GetProperty("reportCount").GetInt32());
        var reasons = row.GetProperty("reasons");
        Assert.Equal(1, reasons.GetArrayLength());
        Assert.Equal("Stötande språk", reasons[0].GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Anmalan_UtanMotivering_Ger400()
    {
        var f = await SeedAsync("anmalan-tom");
        var id = await PostAsync(f, CoachToken(f), "Meddelande");

        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages/{id}/report", GuardianToken(f),
            new { reason = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminRadering_UnderTroskeln_Ger409_VidTroskeln_TarBort()
    {
        var f = await SeedAsync("troskel");
        var id = await PostAsync(f, CoachToken(f), "Ifrågasatt meddelande");

        // Två anmälningar räcker inte (tröskeln är tre).
        await ReportAsync(f, GuardianToken(f), id, "Ett");
        await ReportAsync(f, Guardian2Token(f), id, "Två");

        var tooFew = await SendAsync(
            HttpMethod.Delete, $"/api/v1/admin/trupper/{f.TruppId}/chat/messages/{id}", AdminToken(f));
        Assert.Equal(HttpStatusCode.Conflict, tooFew.StatusCode);

        // Den tredje anmälaren låser upp raderingen.
        await ReportAsync(f, CoachToken(f), id, "Tre");

        var removed = await SendAsync(
            HttpMethod.Delete, $"/api/v1/admin/trupper/{f.TruppId}/chat/messages/{id}", AdminToken(f));
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        await AssertAuditedAsync("chatt.meddelande.raderat", id);
    }

    // ---- Notis -----------------------------------------------------------------------

    [Fact]
    public async Task Notis_NarMedlemmar_UtomForfattareOchAvstangda()
    {
        var f = await SeedAsync("notis");
        var (app, outbox) = WithRecordingOutbox();

        // Admin (författaren) postar.
        var post = await SendVia(app, HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages",
            AdminToken(f), new { body = "Hej alla" });
        Assert.Equal(HttpStatusCode.NoContent, post.StatusCode);

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal(PushCategory.Chat, dispatch.Category);
        Assert.NotNull(dispatch.AccountIds);
        Assert.Contains(f.CoachId, dispatch.AccountIds!);
        Assert.Contains(f.GuardianId, dispatch.AccountIds!);
        Assert.DoesNotContain(f.AdminId, dispatch.AccountIds!); // författaren
        Assert.DoesNotContain(f.Guardian2Id, dispatch.AccountIds!); // stängt av Chatt
    }

    // ---- Släpp av schemalagt ---------------------------------------------------------

    [Fact]
    public async Task Slapp_PubliceraDeVarsTidPasserat()
    {
        var f = await SeedAsync("slapp");
        var now = DateTime.UtcNow;

        // Ett schemalagt vars tid redan passerat (som om worker inte hunnit köra).
        var messageId = Guid.NewGuid();
        await WithScopeAsync(async context =>
        {
            context.ChatMessages.Add(new ChatMessage
            {
                Id = messageId,
                AgeGroupId = f.TruppId,
                TeamId = null,
                AuthorAccountId = f.AdminId,
                Body = "Släpps nu",
                CreatedUtc = now.AddHours(-2),
                PublishAtUtc = now.AddMinutes(-1),
                PublishedUtc = null,
            });
            await context.SaveChangesAsync(CancellationToken.None);
        });

        // Inte synligt före släpp.
        Assert.Empty(await MessagesAsync(f, AdminToken(f)));

        var (app, _) = WithRecordingOutbox();
        using (var scope = app.Services.CreateScope())
        {
            var chat = scope.ServiceProvider.GetRequiredService<ChatService>();
            var released = await chat.ReleaseDueAsync(CancellationToken.None);
            Assert.Equal(1, released);
        }

        var messages = await MessagesAsync(f, AdminToken(f));
        Assert.Contains(messages, m => m.GetProperty("id").GetGuid() == messageId);
    }

    // ---- Hjälpare --------------------------------------------------------------------

    private async Task<Fixture> SeedAsync(string suffix)
    {
        var now = DateTime.UtcNow;
        var truppId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var teamSlug = $"gul-chat-{suffix}";
        var adminId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var guardianId = Guid.NewGuid();
        var guardian2Id = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();

        await WithScopeAsync(async context =>
        {
            var club = new Club { Id = Guid.NewGuid(), Name = "Kärra", Slug = $"klubb-chat-{suffix}" };
            context.Clubs.Add(club);
            context.AgeGroups.Add(new AgeGroup
            {
                Id = truppId,
                ClubId = club.Id,
                Name = "P2016",
                Season = "2026",
            });
            context.Teams.Add(new Team
            {
                Id = teamId,
                AgeGroupId = truppId,
                Name = "Gul",
                ColorHex = "#D9A21B",
                Slug = teamSlug,
            });

            context.Accounts.AddRange(
                Account(adminId, "admin", suffix, "Admin"),
                Account(coachId, "coach", suffix, "Coach"),
                Account(guardianId, "vh", suffix, "Guardian"),
                Account(guardian2Id, "vh2", suffix, "Guardian2"),
                Account(nonMemberId, "utom", suffix, "Utomstaende"));

            // Admin för truppen och tränare för laget.
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
                    TeamId = teamId,
                    Role = RoleKind.Coach,
                    GrantedUtc = now,
                });

            // Två vårdnadshavare, var sitt barn i laget.
            AddGuardianChild(context, truppId, teamId, guardianId, now, "Liam");
            AddGuardianChild(context, truppId, teamId, guardian2Id, now, "Nova");

            // Guardian2 har stängt av Chatt för laget.
            context.NotificationPreferences.Add(new NotificationPreference
            {
                Id = Guid.NewGuid(),
                AccountId = guardian2Id,
                TeamId = teamId,
                EventChanges = true,
                Kallelser = true,
                Carpool = true,
                Chat = false,
                UpdatedUtc = now,
            });

            await context.SaveChangesAsync(CancellationToken.None);
        });

        return new Fixture(
            truppId, teamId, teamSlug, adminId, coachId, guardianId, guardian2Id, nonMemberId);
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

    private async Task<Guid> PostAsync(Fixture f, string token, string body)
    {
        var post = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages", token, new { body });
        post.EnsureSuccessStatusCode();

        var messages = await MessagesAsync(f, token);
        return messages.Last(m => m.GetProperty("body").GetString() == body).GetProperty("id").GetGuid();
    }

    private async Task<IReadOnlyList<JsonElement>> MessagesAsync(Fixture f, string token) =>
        await ArrayAsync(f, "messages", token);

    private async Task<IReadOnlyList<JsonElement>> ArrayAsync(Fixture f, string path, string token)
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
        Token(f.CoachId, new AccountRoles(false, [], [f.TeamSlug]), "coach@test");

    private string GuardianToken(Fixture f) => Token(f.GuardianId, AccountRoles.None, "vh@test");

    private string Guardian2Token(Fixture f) => Token(f.Guardian2Id, AccountRoles.None, "vh2@test");

    private string NonMemberToken(Fixture f) => Token(f.NonMemberId, AccountRoles.None, "utom@test");

    private async Task ReportAsync(Fixture f, string token, Guid messageId, string reason)
    {
        var response = await SendAsync(
            HttpMethod.Post, $"/api/v1/trupper/{f.TruppId}/chat/messages/{messageId}/report", token,
            new { reason });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

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
