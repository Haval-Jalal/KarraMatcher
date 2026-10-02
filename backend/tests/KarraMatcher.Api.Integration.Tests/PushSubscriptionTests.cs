using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Infrastructure.Persistence;
using KarraMatcher.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Notisprenumerationer (`#60`, §KM.3, §KM.10) — <b>per enhet</b> (`#332`-uppföljning).
///
/// <para>
/// En prenumeration hör till <em>webbläsaren</em>, inte till ett lag: högst en rad per enhet
/// (unikt index på adressen), knuten till det inloggade kontot. Att slå på notiser flera gånger —
/// eller förr, för flera lag — ger inte längre flera rader och därmed flera identiska notiser.
/// Kräver inloggning; en gäst nekas. Adressen kommer aldrig tillbaka i ett svar (§KM.10).
/// </para>
/// </summary>
public sealed class PushSubscriptionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static string EndpointFor(string suffix) =>
        $"https://fcm.googleapis.com/fcm/send/{suffix}-hemlig-adress";

    private static object Subscription(string endpoint) => new
    {
        endpoint,
        p256dh = "BLc4xRzKlKORKWlbdgFaBrrPK3ydWAHo4M0gs0i1oEKgPpWG5nnwyPCwbLwGvHqvqnfHiPSw1kvR8t9zs2VoXsc",
        auth = "8eDyX_uCN0XRhSbY5hs7Hg",
    };

    private async Task<Guid> SeedAccountAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"foralder-{suffix}@example.com",
            CreatedUtc = DateTime.UtcNow,
        };
        context.Accounts.Add(account);
        await context.SaveChangesAsync(CancellationToken.None);

        return account.Id;
    }

    private async Task<int> CountAsync(string endpoint)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.PushSubscriptions
            .AsNoTracking()
            .CountAsync(s => s.Endpoint == endpoint, CancellationToken.None);
    }

    private async Task<Guid?> AccountIdOfAsync(string endpoint)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.Endpoint == endpoint)
            .Select(s => s.AccountId)
            .FirstAsync(CancellationToken.None);
    }

    private string TokenFor(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [], [])).Token;
    }

    /// <summary>En klient inloggad som ett visst konto (Bearer). Push kräver ingen CSRF.</summary>
    private HttpClient AccountClient(Guid accountId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TokenFor(accountId));
        return client;
    }

    [Fact]
    public async Task Prenumeration_SomInloggad_KnyterKontot()
    {
        var accountId = await SeedAccountAsync("linked");
        var endpoint = EndpointFor("linked");

        using var client = AccountClient(accountId);
        var response = await client.PostAsJsonAsync(
            "/api/v1/push", Subscription(endpoint), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await CountAsync(endpoint));
        Assert.Equal(accountId, await AccountIdOfAsync(endpoint));
    }

    [Fact]
    public async Task Prenumeration_UtanInloggning_Nekas()
    {
        var endpoint = EndpointFor("anon");

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/v1/push", Subscription(endpoint), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(0, await CountAsync(endpoint));
    }

    [Fact]
    public async Task Prenumeration_FleraGanger_GerEnRad()
    {
        // Kärnan i `#332`-uppföljningen: samma enhet slår på notiser flera gånger (förr en gång per
        // lag) → en enda rad, så samma flyttade match inte ger flera identiska notiser.
        var accountId = await SeedAccountAsync("upprepad");
        var endpoint = EndpointFor("upprepad");

        using var client = AccountClient(accountId);

        await client.PostAsJsonAsync("/api/v1/push", Subscription(endpoint), CancellationToken.None);
        await client.PostAsJsonAsync("/api/v1/push", Subscription(endpoint), CancellationToken.None);
        await client.PostAsJsonAsync("/api/v1/push", Subscription(endpoint), CancellationToken.None);

        Assert.Equal(1, await CountAsync(endpoint));
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/inte-https")]
    [InlineData("/relativ/adress")]
    [InlineData("")]
    public async Task Prenumeration_MedOrimligAdress_Avvisas(string endpoint)
    {
        var accountId = await SeedAccountAsync($"adress-{endpoint.Length}");

        using var client = AccountClient(accountId);
        var response = await client.PostAsJsonAsync(
            "/api/v1/push", Subscription(endpoint), CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Avregistrering_TarBortRaden()
    {
        var accountId = await SeedAccountAsync("bort");
        var endpoint = EndpointFor("bort");

        using var client = AccountClient(accountId);
        await client.PostAsJsonAsync("/api/v1/push", Subscription(endpoint), CancellationToken.None);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push")
        {
            Content = JsonContent.Create(new { endpoint }),
        };
        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountAsync(endpoint));
    }

    [Fact]
    public async Task Avregistrering_AvNagotSomInteFinns_GerSammaSvar()
    {
        // Ett annat svar hade avslöjat om en adress är känd hos oss.
        var accountId = await SeedAccountAsync("okand-bort");

        using var client = AccountClient(accountId);
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push")
        {
            Content = JsonContent.Create(new { endpoint = EndpointFor("aldrig-sedd") }),
        };
        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Avregistrering_AvEnAnnansAdress_RorInteRaden()
    {
        // Objektnivå (§KM.3, #550): push-adressen är en webbläsarhemlighet, men skulle någon få tag
        // i en annans adress får hen inte avregistrera den. Raderingen scope:as till eget konto.
        var ownerId = await SeedAccountAsync("owner");
        var attackerId = await SeedAccountAsync("attacker");
        var endpoint = EndpointFor("owned");

        using (var owner = AccountClient(ownerId))
        {
            await owner.PostAsJsonAsync(
                "/api/v1/push", Subscription(endpoint), CancellationToken.None);
        }

        // En annan inloggad försöker avregistrera ägarens adress → 204 (avslöjar inget), men raden
        // står kvar.
        using var attacker = AccountClient(attackerId);
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/push")
        {
            Content = JsonContent.Create(new { endpoint }),
        };
        var response = await attacker.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await CountAsync(endpoint));
        Assert.Equal(ownerId, await AccountIdOfAsync(endpoint));
    }

    [Fact]
    public async Task Adressen_KommerAldrigTillbakaISvaret()
    {
        // §KM.10: adressen identifierar en enhet lika bra som ett telefonnummer.
        var accountId = await SeedAccountAsync("tystnad");
        var endpoint = EndpointFor("tystnad");

        using var client = AccountClient(accountId);
        var response = await client.PostAsJsonAsync(
            "/api/v1/push", Subscription(endpoint), CancellationToken.None);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("tystnad-hemlig-adress", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adressen_HamnarAldrigIAuditloggen()
    {
        var accountId = await SeedAccountAsync("audit");
        var endpoint = EndpointFor("audit");

        using var client = AccountClient(accountId);
        await client.PostAsJsonAsync("/api/v1/push", Subscription(endpoint), CancellationToken.None);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var details = await context.AuditEntries
            .AsNoTracking()
            .Select(entry => entry.Details)
            .ToListAsync(CancellationToken.None);

        Assert.All(
            details,
            detail => Assert.DoesNotContain(
                "audit-hemlig-adress", detail ?? string.Empty, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nyckel_UtanInloggning_Nekas()
    {
        // Stängd app (§KM.3): även VAPID-nyckeln ligger bakom inloggning i v2.
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/push/key", CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublikNyckel_UtanKonfiguration_SagerAttPushArAv()
    {
        // Testmiljön har inga VAPID-nycklar: appen ska gå att köra utan push. 404 + text, inte 500.
        using var client = factory.CreateSuperAdminClient();

        var response = await client.GetAsync("/api/v1/push/key", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void VapidNycklar_HarRattForm()
    {
        // 65 byte för den publika (0x04 + X + Y), 32 för den privata — webbläsarens
        // applicationServerKey accepterar ingenting annat.
        var (publicKey, privateKey) = VapidKeys.Generate();

        var point = Base64UrlDecode(publicKey);
        var scalar = Base64UrlDecode(privateKey);

        Assert.Equal(65, point.Length);
        Assert.Equal(0x04, point[0]);
        Assert.Equal(32, scalar.Length);
    }

    [Fact]
    public void VapidNycklar_ArNyaVarjeGang()
    {
        var first = VapidKeys.Generate();
        var second = VapidKeys.Generate();

        Assert.NotEqual(first.PrivateKey, second.PrivateKey);
        Assert.NotEqual(first.PublicKey, second.PublicKey);
    }

    [Fact]
    public void PrivataNyckeln_LiggerAldrigIFrontendensMiljofil()
    {
        var path = Path.Combine(RepositoryRoot(), "frontend", ".env.example");

        var text = File.Exists(path) ? File.ReadAllText(path) : string.Empty;

        Assert.DoesNotContain("PrivateKey", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VAPID_PRIVATE", text, StringComparison.OrdinalIgnoreCase);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '='));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }
}
