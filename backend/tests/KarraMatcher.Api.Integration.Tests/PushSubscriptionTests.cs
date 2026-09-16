using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;
using KarraMatcher.Infrastructure.Security;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Notisprenumerationer (`#60`, §KM.3, §KM.10).
///
/// <para>
/// Två saker prövas. Att prenumerationen fungerar <b>utan konto</b> — kravet är inte
/// bekvämlighet utan räckvidd: kräver notiser en inloggning når de en bråkdel av
/// föräldrarna, och att lördagens match är inställd ska nå alla. Och att push-adressen
/// aldrig kommer tillbaka i ett svar; den identifierar en enskild enhet lika bra som ett
/// telefonnummer.
/// </para>
/// </summary>
public sealed class PushSubscriptionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/abc123-hemlig-adress";

    private static object Subscription(string endpoint = Endpoint) => new
    {
        endpoint,
        p256dh = "BLc4xRzKlKORKWlbdgFaBrrPK3ydWAHo4M0gs0i1oEKgPpWG5nnwyPCwbLwGvHqvqnfHiPSw1kvR8t9zs2VoXsc",
        auth = "8eDyX_uCN0XRhSbY5hs7Hg",
    };

    private async Task<string> SeedTeamAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-p-{suffix}" };
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
            Slug = $"gul-p-{suffix}",
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);

        await context.SaveChangesAsync(CancellationToken.None);

        return team.Slug;
    }

    private async Task<int> CountAsync(string slug)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.PushSubscriptions
            .AsNoTracking()
            .CountAsync(
                s => context.Teams.Any(team => team.Id == s.TeamId && team.Slug == slug),
                CancellationToken.None);
    }

    private async Task<(string Slug, Guid AccountId)> SeedTeamWithAccountAsync(string suffix)
    {
        var slug = await SeedTeamAsync(suffix);

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

        return (slug, account.Id);
    }

    private string TokenFor(Guid accountId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(accountId, "konto@example.com", new AccountRoles(false, [], [])).Token;
    }

    private async Task<Guid?> AccountIdOfAsync(string slug, string endpoint)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        // Filtrerat pa laget: endpoint-konstanten delas mellan testen, och det unika indexet
        // ar (TeamId, Endpoint) -- utan slugen kunde en annan tests rad matchas.
        return await context.PushSubscriptions
            .AsNoTracking()
            .Where(s => s.Endpoint == endpoint
                && context.Teams.Any(team => team.Id == s.TeamId && team.Slug == slug))
            .Select(s => s.AccountId)
            .FirstAsync(CancellationToken.None);
    }

    // ---- Kontokoppling (#63) ----------------------------------------------------------

    [Fact]
    public async Task Prenumeration_MedInloggning_KnyterKontot()
    {
        // #63: en inloggad webblasares prenumeration knyts till kontot, sa samakningsnotiser
        // kan na just den foraldern. Adressen kommer aldrig tillbaka -- kopplingen kollas i DB.
        var (slug, accountId) = await SeedTeamWithAccountAsync("linked");

        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/teams/{slug}/push")
        {
            Content = JsonContent.Create(Subscription()),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TokenFor(accountId));

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(accountId, await AccountIdOfAsync(slug, Endpoint));
    }

    [Fact]
    public async Task Prenumeration_SomGast_HarIngenKontokoppling()
    {
        // En gast prenumererar precis som forr -- ingen koppling, och far anda lagets notiser.
        var slug = await SeedTeamAsync("guest-link");

        using var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            $"/api/v1/teams/{slug}/push", Subscription(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await AccountIdOfAsync(slug, Endpoint));
    }

    // ---- Utan konto ------------------------------------------------------------------

    [Fact]
    public async Task Prenumeration_UtanInloggning_Fungerar()
    {
        /*
         * Karnan i #60. Kravet ar inte bekvamlighet utan rackvidd -- det har ar den enda
         * skrivningen i appen som med avsikt star oppen (§KM.3), och undantaget ar infort
         * i GuestAccessTests med sitt skal.
         */
        var slug = await SeedTeamAsync("anon");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/teams/{slug}/push",
            Subscription(),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await CountAsync(slug));
    }

    [Fact]
    public async Task Prenumeration_TvaGanger_GerEnRad()
    {
        // En foralder som laddar om sidan ska inte fa tva notiser for samma flyttade match.
        var slug = await SeedTeamAsync("upprepad");

        using var client = factory.CreateClient();

        await client.PostAsJsonAsync($"/api/v1/teams/{slug}/push", Subscription(), CancellationToken.None);
        await client.PostAsJsonAsync($"/api/v1/teams/{slug}/push", Subscription(), CancellationToken.None);

        Assert.Equal(1, await CountAsync(slug));
    }

    [Fact]
    public async Task Prenumeration_ForOkantLag_GerFyrahundrafyra()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/teams/finns-inte/push",
            Subscription(),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("http://fcm.googleapis.com/inte-https")]
    [InlineData("/relativ/adress")]
    [InlineData("")]
    public async Task Prenumeration_MedOrimligAdress_Avvisas(string endpoint)
    {
        // Vardet kommer fran en klient. En relativ eller http-adress ar antingen ett
        // trasigt anrop eller nagon som provar vad servern accepterar.
        var slug = await SeedTeamAsync($"adress-{endpoint.Length}");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/teams/{slug}/push",
            Subscription(endpoint),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Avregistrering --------------------------------------------------------------

    [Fact]
    public async Task Avregistrering_TarBortRaden()
    {
        var slug = await SeedTeamAsync("bort");

        using var client = factory.CreateClient();

        await client.PostAsJsonAsync($"/api/v1/teams/{slug}/push", Subscription(), CancellationToken.None);

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/teams/{slug}/push")
        {
            Content = JsonContent.Create(new { endpoint = Endpoint }),
        };

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await CountAsync(slug));
    }

    [Fact]
    public async Task Avregistrering_AvNagotSomInteFinns_GerSammaSvar()
    {
        // Ett annat svar hade avslojat om en adress ar kand hos oss.
        var slug = await SeedTeamAsync("okand-bort");

        using var client = factory.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/teams/{slug}/push")
        {
            Content = JsonContent.Create(new { endpoint = "https://fcm.googleapis.com/aldrig-sedd" }),
        };

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    // ---- Adressen lamnar aldrig servern ----------------------------------------------

    [Fact]
    public async Task Adressen_KommerAldrigTillbakaISvaret()
    {
        /*
         * §KM.10. Push-adressen identifierar en enskild enhet lika bra som ett
         * telefonnummer. Att den skickades in av samma klient gor den inte ofarlig att
         * skicka tillbaka -- svaret kan hamna i en proxy-logg eller en felrapport.
         */
        var slug = await SeedTeamAsync("tystnad");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/api/v1/teams/{slug}/push",
            Subscription(),
            CancellationToken.None);

        var body = await response.Content.ReadAsStringAsync(CancellationToken.None);

        Assert.DoesNotContain("abc123-hemlig-adress", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adressen_HamnarAldrigIAuditloggen()
    {
        var slug = await SeedTeamAsync("audit");

        using var client = factory.CreateClient();

        await client.PostAsJsonAsync($"/api/v1/teams/{slug}/push", Subscription(), CancellationToken.None);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var details = await context.AuditEntries
            .AsNoTracking()
            .Select(entry => entry.Details)
            .ToListAsync(CancellationToken.None);

        Assert.All(
            details,
            detail => Assert.DoesNotContain(
                "abc123-hemlig-adress",
                detail ?? string.Empty,
                StringComparison.Ordinal));
    }

    // ---- Nyckeln -----------------------------------------------------------------------

    [Fact]
    public async Task PublikNyckel_UtanKonfiguration_SagerAttPushArAv()
    {
        /*
         * Testmiljon har inga VAPID-nycklar, och det ar ratt lage att prova: appen ska ga
         * att kora utan push. Svaret ar 404 och en text som sager att resten fungerar --
         * inte ett femhundrafel som ser ut som att appen ar trasig.
         */
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/push/key", CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public void VapidNycklar_HarRattForm()
    {
        /*
         * 65 byte for den publika (0x04 plus X och Y) och 32 for den privata. Formatet ar
         * inte vart val -- webblasarens applicationServerKey accepterar ingenting annat, och
         * ett fel har hade upptackts forst nar en foralder tillater notiser.
         */
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
        // En generator som gav samma par tva ganger hade varit varre an ingen generator.
        var first = VapidKeys.Generate();
        var second = VapidKeys.Generate();

        Assert.NotEqual(first.PrivateKey, second.PrivateKey);
        Assert.NotEqual(first.PublicKey, second.PublicKey);
    }

    [Fact]
    public void PrivataNyckeln_LiggerAldrigIFrontendensMiljofil()
    {
        /*
         * Allt i frontendens bundle ar publikt. En privat nyckel dar hade latit vem som
         * helst skicka notiser i appens namn till varje foralder som prenumererar.
         * Kontrollen ar grov med flit: den letar efter namnet, inte efter ett varde.
         */
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

    /// <summary>Letar sig upp till repo-roten från testets utkatalog.</summary>
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
