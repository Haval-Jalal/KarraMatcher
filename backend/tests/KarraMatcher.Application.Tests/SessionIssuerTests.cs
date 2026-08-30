using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Rotation och återanvändningsdetektering (§KM.11, checklistan 1.4 till 1.6).
///
/// <para>
/// Sessionerna i en PWA är långlivade — 60 dagar — så en stulen refresh-token är
/// värdefull länge. Det som gör den kortlivad i praktiken är att varje förnyelse byter ut
/// den, och att en token som dyker upp två gånger fäller hela kedjan. Testerna här är
/// skrivna kring just den mekaniken, eftersom den är osynlig i drift: allt ser ut att
/// fungera ända tills någon faktiskt blivit bestulen.
/// </para>
/// </summary>
public sealed class SessionIssuerTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeRefreshTokenRepository _tokens = new();
    private readonly FakeClock _clock = new(Start);
    private readonly Account _account = new()
    {
        Id = Guid.NewGuid(),
        Email = "foralder@example.com",
        CreatedUtc = Start,
    };

    private SessionIssuer CreateIssuer(AuthOptions? options = null) => new(
        _tokens,
        new StubAccessTokenIssuer(_clock),
        Options.Create(options ?? new AuthOptions
        {
            SigningKey = new string('k', 32),
            AccessTokenLifetime = TimeSpan.FromMinutes(15),
            RefreshTokenLifetime = TimeSpan.FromDays(60),
        }),
        _clock,
        NullLogger<SessionIssuer>.Instance);

    // ---- Utfärdande ------------------------------------------------------------------

    [Fact]
    public async Task Issue_LagrarAldrigTokenIKlartext()
    {
        // En läckt databas ska inte vara en hink med giltiga sessioner.
        var session = await CreateIssuer().IssueAsync(_account, CancellationToken.None);

        var stored = Assert.Single(_tokens.Tokens);

        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Equal(SessionIssuer.Hash(session.RefreshToken), stored.TokenHash);
        Assert.DoesNotContain(session.RefreshToken, stored.TokenHash, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Issue_GerVarjeSessionEnEgenToken()
    {
        var issuer = CreateIssuer();

        var first = await issuer.IssueAsync(_account, CancellationToken.None);
        var second = await issuer.IssueAsync(_account, CancellationToken.None);

        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
    }

    [Fact]
    public async Task Issue_StartarEnNyFamiljPerInloggning()
    {
        // Två telefoner ska kunna vara inloggade utan att den ena loggar ut den andra.
        var issuer = CreateIssuer();

        await issuer.IssueAsync(_account, CancellationToken.None);
        await issuer.IssueAsync(_account, CancellationToken.None);

        Assert.Equal(2, _tokens.Tokens.Select(t => t.FamilyId).Distinct().Count());
    }

    // ---- Rotation --------------------------------------------------------------------

    [Fact]
    public async Task Refresh_GerNyTokenOchMarkerarDenGamlaSomErsatt()
    {
        var issuer = CreateIssuer();
        var first = await issuer.IssueAsync(_account, CancellationToken.None);

        _clock.Advance(TimeSpan.FromMinutes(20));
        var second = await issuer.RefreshAsync(first.RefreshToken, CancellationToken.None);

        Assert.NotNull(second);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);

        var old = _tokens.Tokens.Single(t => t.TokenHash == SessionIssuer.Hash(first.RefreshToken));
        Assert.NotNull(old.ReplacedUtc);
    }

    [Fact]
    public async Task Refresh_BehallerFamiljen()
    {
        // Kedjan måste hålla ihop, annars finns inget att återkalla vid en stöld.
        var issuer = CreateIssuer();
        var first = await issuer.IssueAsync(_account, CancellationToken.None);

        await issuer.RefreshAsync(first.RefreshToken, CancellationToken.None);

        Assert.Single(_tokens.Tokens.Select(t => t.FamilyId).Distinct());
    }

    [Fact]
    public async Task Refresh_OkandToken_GerNull()
    {
        var result = await CreateIssuer().RefreshAsync("hittepa", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Refresh_UtgangenToken_GerNull()
    {
        var issuer = CreateIssuer();
        var session = await issuer.IssueAsync(_account, CancellationToken.None);

        _clock.Advance(TimeSpan.FromDays(61));

        Assert.Null(await issuer.RefreshAsync(session.RefreshToken, CancellationToken.None));
    }

    // ---- Stöldskyddet ----------------------------------------------------------------

    [Fact]
    public async Task Refresh_AteranvandToken_AterkallarHelaFamiljen()
    {
        /*
         * Scenariot: nagon har kopierat en refresh-token. Bada anvander den -- den
         * riktiga anvandaren forst, tjuven sedan (eller tvartom, det gar inte att veta).
         *
         * Nar den redan bytta token dyker upp igen ar det beviset. Da faller hela kedjan,
         * inklusive den nyaste token som tjuven annars hade haft kvar.
         */
        var issuer = CreateIssuer();
        var stolen = await issuer.IssueAsync(_account, CancellationToken.None);

        var legitimate = await issuer.RefreshAsync(stolen.RefreshToken, CancellationToken.None);
        Assert.NotNull(legitimate);

        // Tjuven anvander sin kopia av den gamla token.
        var thief = await issuer.RefreshAsync(stolen.RefreshToken, CancellationToken.None);

        Assert.Null(thief);
        Assert.All(_tokens.Tokens, token => Assert.NotNull(token.RevokedUtc));
    }

    [Fact]
    public async Task Refresh_EfterUpptacktStold_GarInteAttFortsattaMedDenNyaToken()
    {
        // Det här är poängen med att fälla familjen och inte bara den återanvända token.
        // Utan det hade den bestulne loggats ut medan tjuven fortsatt.
        var issuer = CreateIssuer();
        var stolen = await issuer.IssueAsync(_account, CancellationToken.None);
        var legitimate = await issuer.RefreshAsync(stolen.RefreshToken, CancellationToken.None);

        await issuer.RefreshAsync(stolen.RefreshToken, CancellationToken.None);

        Assert.Null(await issuer.RefreshAsync(legitimate!.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task Refresh_AteranvandToken_RorInteAndraSessioner()
    {
        // En stöld på en telefon ska inte logga ut familjens andra telefon.
        var issuer = CreateIssuer();
        var phone = await issuer.IssueAsync(_account, CancellationToken.None);
        var tablet = await issuer.IssueAsync(_account, CancellationToken.None);

        await issuer.RefreshAsync(phone.RefreshToken, CancellationToken.None);
        await issuer.RefreshAsync(phone.RefreshToken, CancellationToken.None);

        Assert.NotNull(await issuer.RefreshAsync(tablet.RefreshToken, CancellationToken.None));
    }

    // ---- Utloggning ------------------------------------------------------------------

    [Fact]
    public async Task SignOut_AterkallarHelaFamiljen()
    {
        var issuer = CreateIssuer();
        var session = await issuer.IssueAsync(_account, CancellationToken.None);
        var renewed = await issuer.RefreshAsync(session.RefreshToken, CancellationToken.None);

        await issuer.SignOutAsync(renewed!.RefreshToken, CancellationToken.None);

        Assert.All(_tokens.Tokens, token => Assert.NotNull(token.RevokedUtc));
        Assert.Null(await issuer.RefreshAsync(renewed.RefreshToken, CancellationToken.None));
    }

    [Fact]
    public async Task SignOut_OkandToken_GorIngenting()
    {
        // Får aldrig gå att använda för att ta reda på om en token är giltig.
        var issuer = CreateIssuer();
        await issuer.IssueAsync(_account, CancellationToken.None);
        var before = _tokens.SaveCount;

        await issuer.SignOutAsync("hittepa", CancellationToken.None);

        Assert.Equal(before, _tokens.SaveCount);
        Assert.All(_tokens.Tokens, token => Assert.Null(token.RevokedUtc));
    }

    /// <summary>Access-token intresserar inte de här testerna — bara att den finns.</summary>
    private sealed class StubAccessTokenIssuer(TimeProvider clock) : IAccessTokenIssuer
    {
        public (string Token, DateTime ExpiresUtc) Issue(Guid accountId, string email) =>
            ($"access-{accountId}", clock.GetUtcNow().UtcDateTime.AddMinutes(15));
    }
}
