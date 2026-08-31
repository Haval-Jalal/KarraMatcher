using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Inloggning med engångskod (checklistan 1.1, 1.2 och 1.7).
///
/// <para>
/// Koden är sex siffror, alltså en miljon möjligheter. Det är i sig inget skydd — det som
/// gör den försvarbar är att den lever i tio minuter, går att använda en gång, och dör
/// efter fem felgissningar. Testerna nedan låser alla tre samtidigt, eftersom var och en
/// för sig är otillräcklig.
/// </para>
///
/// <para>
/// Den andra halvan är vad flödet <em>inte</em> avslöjar. En inloggningsruta som svarar
/// olika för en känd och en okänd adress är en adresslista för den som frågar tillräckligt
/// många gånger.
/// </para>
/// </summary>
public sealed class LoginCodeServiceTests
{
    private static readonly DateTime Start = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    private readonly FakeLoginCodeRepository _codes = new();
    private readonly FakeAccountRepository _accounts = new();
    private readonly FakeEmailSender _email = new();
    private readonly FakeRefreshTokenRepository _tokens = new();
    private readonly FakeClock _clock = new(Start);

    private readonly AuthOptions _options = new()
    {
        SigningKey = new string('k', 32),
        LoginCodeLifetime = TimeSpan.FromMinutes(10),
        MaxLoginCodeAttempts = 5,
        LoginCodeResendCooldown = TimeSpan.FromSeconds(60),
    };

    private LoginCodeService CreateService() => new(
        _codes,
        _accounts,
        _email,
        new SessionIssuer(
            _tokens,
            new StubTokenIssuer(),
            Options.Create(_options),
            _clock,
            NullLogger<SessionIssuer>.Instance),
        Options.Create(_options),
        _clock);

    /// <summary>Koden som just skickades, läst ur mejlet — som en förälder gör.</summary>
    private string SentCode() =>
        new(_email.LastBody!.Where(char.IsAsciiDigit).Take(6).ToArray());

    // ---- Koden -----------------------------------------------------------------------

    [Fact]
    public async Task Request_SkickarEnSexsiffrigKod()
    {
        await CreateService().RequestAsync("Foralder@Example.COM", CancellationToken.None);

        Assert.Equal("foralder@example.com", _email.LastRecipient);
        Assert.Matches("[0-9]{6}", _email.LastBody);
    }

    [Fact]
    public async Task Request_LagrarKodenHashad()
    {
        // Samma resonemang som för refresh-tokens: en läckt databas ska inte innehålla
        // giltiga inloggningar.
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        var stored = Assert.Single(_codes.Codes);

        Assert.NotEqual(SentCode(), stored.CodeHash);
        Assert.Equal(LoginCodeService.Hash(SentCode()), stored.CodeHash);
    }

    [Fact]
    public void Generate_GerVarierandeKoderIHelaIntervallet()
    {
        /*
         * Grov kontroll av att kallan faktiskt slumpar. En trasig generator -- en fast
         * kod, eller en som alltid borjar pa samma siffra -- ger utslag har.
         *
         * Det har ar inget statistiskt test och ska inte lasas som ett. Det fangar det
         * som gar fel i praktiken: nagon byter till Random, eller till ett fast varde
         * "tills vidare".
         */
        var codes = Enumerable.Range(0, 200)
            .Select(_ => LoginCodeService.GenerateCode())
            .ToArray();

        Assert.All(codes, code => Assert.Matches("^[0-9]{6}$", code));
        Assert.True(codes.Distinct().Count() > 150, "Kodgeneratorn upprepar sig.");
        Assert.True(codes.Select(c => c[0]).Distinct().Count() > 3, "Forsta siffran varierar inte.");
    }

    // ---- Engangsanvandning, utgang och sparr -----------------------------------------

    [Fact]
    public async Task Verify_RattKod_GerSession()
    {
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        var session = await service.VerifyAsync(
            "foralder@example.com", SentCode(), CancellationToken.None);

        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
    }

    [Fact]
    public async Task Verify_SammaKodTvaGanger_FungerarBaraForstaGangen()
    {
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);
        var code = SentCode();

        Assert.NotNull(await service.VerifyAsync("foralder@example.com", code, CancellationToken.None));
        Assert.Null(await service.VerifyAsync("foralder@example.com", code, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_UtgangenKod_Nekas()
    {
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);
        var code = SentCode();

        _clock.Advance(TimeSpan.FromMinutes(11));

        Assert.Null(await service.VerifyAsync("foralder@example.com", code, CancellationToken.None));
    }

    [Fact]
    public async Task Verify_FemFelgissningar_DodarKoden()
    {
        // Det här är vad som gör sex siffror försvarbart. Utan spärren är en miljon
        // möjligheter inget skydd alls.
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);
        var code = SentCode();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Null(await service.VerifyAsync(
                "foralder@example.com", "000000", CancellationToken.None));
        }

        // Även den riktiga koden är nu värdelös.
        Assert.Null(await service.VerifyAsync("foralder@example.com", code, CancellationToken.None));
    }

    [Fact]
    public async Task Request_NyKod_GorDenGamlaOanvandbar()
    {
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);
        var first = SentCode();

        _clock.Advance(TimeSpan.FromMinutes(2));
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        Assert.Null(await service.VerifyAsync("foralder@example.com", first, CancellationToken.None));
        Assert.NotNull(await service.VerifyAsync(
            "foralder@example.com", SentCode(), CancellationToken.None));
    }

    [Fact]
    public async Task Request_NyKod_MarkerarDenGamlaSomForbrukad()
    {
        /*
         * Testet ovan sager att den gamla koden inte gar att anvanda -- men det ar sant
         * anda, eftersom verifieringen bara slar upp den *senaste* koden. Det passerade
         * alltsa aven nar ogiltigforklarandet togs bort, vilket ett prov avslojade.
         *
         * Att aldre koder ar oatkomliga ar i dag en foljd av uppslagningen, inte av
         * datan. Det ar en spro grund: skrivs FindLatestAsync nagon gang om till att
         * soka pa hash blir varje gammal kod levande igen. Darfor markeras de i datan
         * ocksa, och det ar den markningen det har testet vaktar.
         */
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        _clock.Advance(TimeSpan.FromMinutes(2));
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        var oldest = _codes.Codes.OrderBy(c => c.CreatedUtc).First();

        Assert.NotNull(oldest.ConsumedUtc);
    }

    [Fact]
    public async Task Request_InomKarenstiden_SkickarIngetNyttMejl()
    {
        // Skyddar en förälders inkorg från att fyllas av någon som upprepar begäran.
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        _clock.Advance(TimeSpan.FromSeconds(30));
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        Assert.Equal(1, _email.SentCount);
    }

    // ---- Vad flodet inte avslojar ----------------------------------------------------

    [Fact]
    public async Task Request_OkandAdress_BeterSigLikadant()
    {
        // Inget kastas, inget svar skiljer sig. Kontot skapas inte heller — en adress
        // som aldrig loggat in ska inte lämna något efter sig.
        var service = CreateService();

        await service.RequestAsync("finns-inte@example.com", CancellationToken.None);

        Assert.Equal(1, _email.SentCount);
        Assert.Empty(_accounts.Accounts);
    }

    [Fact]
    public async Task Verify_FelKodOchOkandAdress_GerSammaSvar()
    {
        var service = CreateService();
        await service.RequestAsync("foralder@example.com", CancellationToken.None);

        var wrongCode = await service.VerifyAsync(
            "foralder@example.com", "000000", CancellationToken.None);
        var unknownEmail = await service.VerifyAsync(
            "aldrig-sett@example.com", "123456", CancellationToken.None);

        Assert.Null(wrongCode);
        Assert.Null(unknownEmail);
    }

    // ---- Kontot ----------------------------------------------------------------------

    [Fact]
    public async Task Verify_ForstaInloggningen_SkaparKontot()
    {
        var service = CreateService();
        await service.RequestAsync("ny@example.com", CancellationToken.None);

        await service.VerifyAsync("ny@example.com", SentCode(), CancellationToken.None);

        var account = Assert.Single(_accounts.Accounts);
        Assert.Equal("ny@example.com", account.Email);
        Assert.Equal(Start, account.LastSignedInUtc);
    }

    [Fact]
    public async Task Verify_AndraInloggningen_AteranvanderKontot()
    {
        var service = CreateService();

        await service.RequestAsync("foralder@example.com", CancellationToken.None);
        await service.VerifyAsync("foralder@example.com", SentCode(), CancellationToken.None);

        _clock.Advance(TimeSpan.FromDays(30));
        await service.RequestAsync("FORALDER@example.com", CancellationToken.None);
        await service.VerifyAsync("foralder@example.com", SentCode(), CancellationToken.None);

        // Versaler i adressen får inte ge ett andra konto för samma person.
        Assert.Single(_accounts.Accounts);
    }

    private sealed class StubTokenIssuer : IAccessTokenIssuer
    {
        public (string Token, DateTime ExpiresUtc) Issue(Guid accountId, string email) =>
            ($"access-{accountId}", DateTime.UtcNow.AddMinutes(15));
    }

    private sealed class FakeEmailSender : IEmailSender
    {
        public int SentCount { get; private set; }

        public string? LastRecipient { get; private set; }

        public string? LastBody { get; private set; }

        public Task SendAsync(
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken)
        {
            SentCount++;
            LastRecipient = recipient;
            LastBody = body;

            return Task.CompletedTask;
        }
    }

    private sealed class FakeAccountRepository : IAccountRepository
    {
        public List<Account> Accounts { get; } = [];

        public Task<Account?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.FirstOrDefault(a => a.Id == id));

        public Task<Account?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Accounts.FirstOrDefault(a => a.Email == email));

        public Task AddAsync(Account account, CancellationToken cancellationToken)
        {
            Accounts.Add(account);

            return Task.CompletedTask;
        }
    }

    private sealed class FakeLoginCodeRepository : ILoginCodeRepository
    {
        public List<LoginCode> Codes { get; } = [];

        public Task<LoginCode?> FindLatestAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Codes
                .Where(c => c.Email == email)
                .OrderByDescending(c => c.CreatedUtc)
                .FirstOrDefault());

        public Task AddAsync(LoginCode code, CancellationToken cancellationToken)
        {
            Codes.Add(code);

            return Task.CompletedTask;
        }

        public Task ConsumeOutstandingAsync(
            string email,
            DateTime nowUtc,
            CancellationToken cancellationToken)
        {
            foreach (var code in Codes.Where(c => c.Email == email && c.ConsumedUtc is null))
            {
                code.ConsumedUtc = nowUtc;
            }

            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
