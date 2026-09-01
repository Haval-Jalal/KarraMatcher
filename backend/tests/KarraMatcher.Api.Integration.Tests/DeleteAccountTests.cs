using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Kontoradering (§KM.6, checklistan 1.6 och 9.6).
///
/// <para>
/// Rätten att bli glömd, och förutsättningen för att kunna vara ärlig i integritetstexten.
/// Raderingen sker på riktigt och inte som en markering — en "raderad"-flagga är inte att
/// bli glömd, det är att bli ihågkommen med en anteckning.
/// </para>
/// </summary>
public sealed class DeleteAccountTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    /// <summary>Skapar ett konto med session, roll och en utestående kod.</summary>
    private async Task<(Guid AccountId, string Email, SessionTokens Session)> CreateAccountAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var issuer = scope.ServiceProvider.GetRequiredService<SessionIssuer>();

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            CreatedUtc = DateTime.UtcNow,
        };

        context.Accounts.Add(account);
        context.LoginCodes.Add(new LoginCode
        {
            Id = Guid.NewGuid(),
            Email = account.Email,
            CodeHash = new string('a', 64),
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.AddMinutes(10),
        });

        await context.SaveChangesAsync(CancellationToken.None);

        var session = await issuer.IssueAsync(account, CancellationToken.None);

        return (account.Id, account.Email, session);
    }

    /// <summary>
    /// Hämtar anti-forgery-token <em>och</em> dess cookie.
    ///
    /// <para>
    /// Båda skickas sedan uttryckligen. Att förlita sig på klientens cookiebehållare
    /// fungerar för POST men inte här, och felsökningen av <em>varför</em> hör inte hemma
    /// i ett test — det som ska prövas är raderingen, inte HttpClient.
    /// </para>
    /// </summary>
    private static async Task<(string Token, string Cookie)> GetCsrfAsync(
        HttpClient client,
        string? accessToken = null)
    {
        /*
         * Med samma token som anropet sedan bar.
         *
         * ASP.NET binder anti-forgery-token till anvandarens identitet. En token hamtad
         * utloggad galler darfor inte for ett inloggat anrop -- det ar avsiktligt och bra,
         * men det betyder att klienten maste hamta en ny token nar inloggningen andras.
         * Just det gav 400 pa varje radering innan det upptacktes har.
         */
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");

        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);

        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        return (body.GetProperty("token").GetString()!, cookie);
    }

    private async Task<HttpResponseMessage> DeleteAsync(SessionTokens session)
    {
        using var client = factory.CreateClient(ClientOptions);
        var (token, cookie) = await GetCsrfAsync(client, session.AccessToken);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account");
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Headers.Add("Cookie", cookie);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        return await client.SendAsync(request, CancellationToken.None);
    }

    // ---- Allt som ags av kontot forsvinner --------------------------------------------

    [Fact]
    public async Task Radering_TarBortKontotPaRiktigt()
    {
        var (accountId, _, session) = await CreateAccountAsync();

        var response = await DeleteAsync(session);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Null(await context.Accounts.FindAsync([accountId], CancellationToken.None));
    }

    /*
     * Att sessionerna forsvinner provas som *beteende* i Radering_GerIngenNySession, och
     * som *konfiguration* i AlltSomPekarPaEttKonto_ForsvinnerMedDet.
     *
     * En radnivakontroll har hade i stallet matt EF:s minnesprovider, som bara kaskaderar
     * rader den redan laddat -- till skillnad fran Postgres, som gor det i databasen.
     * Testet hade alltsa fallit utan att nagot var fel, eller gatt gront av fel skal.
     */

    [Fact]
    public async Task Radering_TarMedSigEngangskoderna()
    {
        /*
         * Det har ar det som ar latt att glomma. Koderna hanger pa adressen och inte pa
         * kontot -- kontot finns ju inte nar koden skickas -- sa de foljer inte med i
         * kaskaden och maste tas bort uttryckligen.
         *
         * Utan det hade en raderad adress lamnat kvar en giltig kod, och den som fortfarande
         * hade mejlet kunnat logga in och skapa kontot pa nytt.
         */
        var (_, email, session) = await CreateAccountAsync();

        await DeleteAsync(session);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        Assert.Empty(await context.LoginCodes
            .Where(c => c.Email == email)
            .ToListAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Radering_GerIngenNySession()
    {
        var (_, _, session) = await CreateAccountAsync();

        await DeleteAsync(session);

        using var client = factory.CreateClient(ClientOptions);
        var (token, cookie) = await GetCsrfAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Headers.Add("Cookie", $"{cookie}; karra_refresh={session.RefreshToken}");

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Audit -----------------------------------------------------------------------

    [Fact]
    public async Task Radering_AuditLoggasOchPostenOverlever()
    {
        // Posten om en radering måste finnas kvar efter att kontot är borta — annars
        // raderar man bort beviset på att raderingen skedde.
        var (accountId, _, session) = await DeleteAndReadAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entry = await context.AuditEntries
            .SingleOrDefaultAsync(e => e.ActorAccountId == accountId, CancellationToken.None);

        Assert.NotNull(entry);
        Assert.Equal(AuditActions.AccountDeleted, entry.Action);
        Assert.Null(await context.Accounts.FindAsync([accountId], CancellationToken.None));
        Assert.NotNull(session);
    }

    [Fact]
    public async Task AuditPosten_InnehallerIngenAdress()
    {
        // §KM.10: aldrig e-post i loggar. En audit-logg som samlar adresser är själv ett
        // integritetsproblem, och den överlever dessutom raderingen den beskriver.
        var (accountId, email, _) = await DeleteAndReadAsync();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var entry = await context.AuditEntries
            .SingleAsync(e => e.ActorAccountId == accountId, CancellationToken.None);

        Assert.DoesNotContain(email, entry.Action, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("@", entry.Action, StringComparison.Ordinal);
    }

    private async Task<(Guid AccountId, string Email, SessionTokens Session)> DeleteAndReadAsync()
    {
        var created = await CreateAccountAsync();

        await DeleteAsync(created.Session);

        return created;
    }

    // ---- Behorighet ------------------------------------------------------------------

    [Fact]
    public async Task Radering_UtanInloggning_Nekas()
    {
        using var client = factory.CreateClient(ClientOptions);
        var (token, cookie) = await GetCsrfAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account");
        request.Headers.Add("X-CSRF-TOKEN", token);
        request.Headers.Add("Cookie", cookie);

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Radering_UtanCsrfToken_Nekas()
    {
        var (_, _, session) = await CreateAccountAsync();

        using var client = factory.CreateClient(ClientOptions);

        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/auth/account");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);

        var response = await client.SendAsync(request, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- Vakten for det som inte finns an ---------------------------------------------

    [Fact]
    public void AlltSomPekarPaEttKonto_ForsvinnerMedDet()
    {
        /*
         * Framtidssakringen. Samakningserbjudanden (M5) och push-prenumerationer (M7)
         * finns inte an, men de kommer att peka pa ett konto -- och §KM.6 kraver att de
         * forsvinner med det.
         *
         * I stallet for att lita pa att nagon minns det den dagen raknar testet upp varje
         * frammande nyckel mot Accounts och kraver kaskad. Den som lagger till en tabell
         * utan det far reda pa det direkt, av ett test som skrevs innan tabellen fanns.
         *
         * Undantaget ar audit-loggen, som med flit saknar frammande nyckel och darfor
         * inte dyker upp har alls.
         */
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var offenders = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Account))
            .Where(fk => fk.DeleteBehavior != DeleteBehavior.Cascade)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}.{fk.Properties[0].Name}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Följande pekar på ett konto utan att försvinna med det (§KM.6): "
                + string.Join(", ", offenders));
    }
}
