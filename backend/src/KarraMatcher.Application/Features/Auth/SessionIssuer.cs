using System.Security.Cryptography;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Domain.Accounts;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Features.Auth;

/// <summary>
/// Utfärdar, roterar och återkallar sessioner.
///
/// <para>
/// Samlat på ett ställe därför att reglerna hänger ihop: rotationen är meningslös utan
/// återanvändningsdetekteringen, och detekteringen är meningslös utan familjen. Ligger de
/// i var sin handler går det att ändra den ena och tro att den andra fortfarande gäller.
/// </para>
///
/// <para>
/// Inloggningen finns inte här — den hör till <c>#29</c>. Det som finns är
/// <see cref="IssueAsync"/>, som inloggningen kommer att anropa när koden är verifierad.
/// </para>
/// </summary>
public sealed partial class SessionIssuer(
    IRefreshTokenRepository tokens,
    IAccessTokenIssuer accessTokens,
    IOptions<AuthOptions> options,
    TimeProvider clock,
    ILogger<SessionIssuer> logger)
{
    /// <summary>
    /// 32 slumpade byte, base64url-kodat.
    ///
    /// <para>
    /// Kryptografiskt slumpat och inte en <c>Guid</c>: en Guid är 122 bitar entropi i
    /// bästa fall, och dess format inbjuder till gissningar om att den skulle vara
    /// förutsägbar. 256 bitar från <see cref="RandomNumberGenerator"/> lämnar ingen sådan
    /// fråga öppen.
    /// </para>
    /// </summary>
    private const int TokenBytes = 32;

    private AuthOptions Options => options.Value;

    /// <summary>SHA-256 i gemena hex. Klartexten lagras aldrig.</summary>
    public static string Hash(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        return Convert.ToHexStringLower(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
    }

    /// <summary>
    /// Ny session för ett konto. Startar en ny familj — anropas när någon loggar in.
    /// </summary>
    public async Task<SessionTokens> IssueAsync(Account account, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(account);

        return await IssueInFamilyAsync(account, Guid.NewGuid(), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Byter en refresh-token mot en ny.
    ///
    /// <para>
    /// Svarar <c>null</c> på allt som inte går: okänd token, utgången token, återkallad
    /// familj — och på en redan förbrukad token, som dessutom fäller hela familjen. Att
    /// alla utfall ser likadana ut utåt är avsiktligt; anroparen ska inte kunna avgöra
    /// <em>varför</em> det gick fel.
    /// </para>
    /// </summary>
    public async Task<SessionTokens?> RefreshAsync(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var stored = await tokens.FindByHashAsync(Hash(rawToken), cancellationToken)
            .ConfigureAwait(false);

        if (stored?.Account is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        if (stored.ReplacedUtc is not null)
        {
            /*
             * En redan bytt token kommer tillbaka. Den finns alltså på två ställen: hos
             * den som bytte den, och hos någon annan. Vem som är vem går inte att avgöra,
             * så hela familjen faller och båda får logga in igen.
             *
             * Loggas med konto-id och familj, aldrig med adress eller token (§KM.10).
             */
            LogTokenReuse(logger, stored.FamilyId, stored.AccountId);

            await tokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken)
                .ConfigureAwait(false);
            await tokens.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }

        if (!stored.IsActive(now))
        {
            return null;
        }

        stored.ReplacedUtc = now;

        return await IssueInFamilyAsync(stored.Account, stored.FamilyId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Loggar ut: hela familjen återkallas, inte bara den token som skickades in.
    ///
    /// <para>
    /// Att logga ut ska betyda att sessionen är slut, inte att just den senaste token
    /// är det. Svarar tyst även för en okänd token — en utloggning ska aldrig kunna
    /// användas för att ta reda på om en token är giltig.
    /// </para>
    /// </summary>
    public async Task SignOutAsync(string? rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var stored = await tokens.FindByHashAsync(Hash(rawToken), cancellationToken)
            .ConfigureAwait(false);

        if (stored is null)
        {
            return;
        }

        await tokens
            .RevokeFamilyAsync(stored.FamilyId, clock.GetUtcNow().UtcDateTime, cancellationToken)
            .ConfigureAwait(false);
        await tokens.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Källgenererad loggrad. Aldrig adress och aldrig tokenvärdet — bara id (§KM.10).
    /// </summary>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Aterandvand refresh-token upptackt. Familjen {FamilyId} for konto {AccountId} aterkallas.")]
    private static partial void LogTokenReuse(ILogger logger, Guid familyId, Guid accountId);

    private async Task<SessionTokens> IssueInFamilyAsync(
        Account account,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(TokenBytes))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var refreshExpires = now.Add(Options.RefreshTokenLifetime);

        await tokens.AddAsync(
            new RefreshToken
            {
                Id = Guid.NewGuid(),
                AccountId = account.Id,

                // Navigeringen sätts, inte bara den främmande nyckeln. Kontot är redan
                // spårat av anroparen, så EF gör ingen extra skrivning — och en token som
                // just skapats vet därmed vem den tillhör utan en ny uppslagning.
                Account = account,

                TokenHash = Hash(raw),
                FamilyId = familyId,
                CreatedUtc = now,
                ExpiresUtc = refreshExpires,
            },
            cancellationToken).ConfigureAwait(false);

        await tokens.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var (accessToken, accessExpires) = accessTokens.Issue(account.Id, account.Email);

        return new SessionTokens(accessToken, accessExpires, raw, refreshExpires);
    }
}
