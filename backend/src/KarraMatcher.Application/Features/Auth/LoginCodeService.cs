using System.Globalization;
using System.Security.Cryptography;

using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Features.Auth;

/// <summary>
/// Begär och verifierar engångskoder.
///
/// <para>
/// Samlat på ett ställe eftersom reglerna hakar i varandra: koden är kort och därmed
/// gissningsbar, vilket bara är försvarbart så länge spärren, utgången och engångs-
/// användningen alla gäller samtidigt. Ligger de utspridda går det att ta bort en av dem
/// och tro att de andra räcker.
/// </para>
///
/// <para>
/// <b>Genomgående regel: svaret utåt är detsamma oavsett vad som händer här inne.</b>
/// Adressen kan vara känd eller okänd, koden kan ha skickats eller inte, mejlet kan ha
/// gått fram eller inte — den som frågar ska inte kunna avgöra vilket. En inloggningsruta
/// som avslöjar vilka adresser som finns är en adresslista för den som frågar tillräckligt
/// många gånger.
/// </para>
/// </summary>
public sealed class LoginCodeService(
    ILoginCodeRepository codes,
    IAccountRepository accounts,
    IEmailSender email,
    SessionIssuer sessions,
    IOptions<AuthOptions> options,
    TimeProvider clock)
{
    /// <summary>
    /// Sex siffror.
    ///
    /// <para>
    /// Valt för att det ska gå att läsa i ett mejl och skriva av på en telefon med en
    /// hand, med ett barn i den andra. Entropin är låg — en miljon möjligheter — och
    /// kompenseras av spärren på fem försök och tio minuters livstid, inte av längden.
    /// </para>
    /// </summary>
    private const int CodeDigits = 6;

    private AuthOptions Options => options.Value;

    /// <summary>Normaliserad adress. Samma adress oavsett skiftläge och kringrymd.</summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>SHA-256 i gemena hex, samma som för refresh-tokens.</summary>
    public static string Hash(string code) => SessionIssuer.Hash(code);

    /// <summary>
    /// Slumpar en kod ur en kryptografisk källa.
    ///
    /// <para>
    /// <see cref="RandomNumberGenerator.GetInt32(int, int)"/> och inte
    /// <c>Random</c>: den senare är förutsägbar från tidigare utfall, vilket är precis vad
    /// en angripare skulle utnyttja. Metoden ger dessutom ett jämnt fördelat tal utan den
    /// snedvridning en modulo-operation hade infört.
    /// </para>
    /// </summary>
    internal static string GenerateCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000)
            .ToString(CultureInfo.InvariantCulture)
            .PadLeft(CodeDigits, '0');

    /// <summary>
    /// Skickar en kod till adressen — om reglerna tillåter det.
    ///
    /// <para>
    /// Svarar aldrig med något. Anroparen ska inte kunna avgöra om ett mejl gick iväg,
    /// om adressen är känd sedan tidigare, eller om en kod redan var på väg.
    /// </para>
    /// </summary>
    public async Task RequestAsync(string emailAddress, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        var normalized = Normalize(emailAddress);
        var now = clock.GetUtcNow().UtcDateTime;

        var latest = await codes.FindLatestAsync(normalized, cancellationToken).ConfigureAwait(false);

        if (latest is not null
            && latest.ConsumedUtc is null
            && latest.CreatedUtc.Add(Options.LoginCodeResendCooldown) > now)
        {
            // En kod är redan på väg. Att skicka en till hade fyllt inkorgen på begäran
            // av vem som helst — och den som frågar märker ingen skillnad.
            return;
        }

        // Tidigare koder dör när en ny begärs. Annars hade varje begäran gett angriparen
        // fem försök till, mot en ny kod, utan att de gamla slutade gälla.
        await codes.ConsumeOutstandingAsync(normalized, now, cancellationToken).ConfigureAwait(false);

        var code = GenerateCode();

        await codes.AddAsync(
            new LoginCode
            {
                Id = Guid.NewGuid(),
                Email = normalized,
                CodeHash = Hash(code),
                CreatedUtc = now,
                ExpiresUtc = now.Add(Options.LoginCodeLifetime),
            },
            cancellationToken).ConfigureAwait(false);

        await codes.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var minutes = (int)Options.LoginCodeLifetime.TotalMinutes;

        await email.SendAsync(
            normalized,
            "Din inloggningskod till Kärra Matcher",
            $"""
             Din kod är {code}

             Den gäller i {minutes} minuter och kan bara användas en gång.

             Har du inte försökt logga in? Då behöver du inte göra någonting — koden
             fungerar bara tillsammans med den här mejladressen.
             """,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Verifierar en kod och ger en session.
    ///
    /// <para>
    /// Svarar <c>null</c> på allt som inte går: fel kod, utgången kod, redan använd kod,
    /// för många försök, eller en adress ingen kod skickats till. Alla ser likadana ut
    /// utåt.
    /// </para>
    ///
    /// <para>
    /// Kontot skapas här, vid första lyckade inloggningen. Fram till dess finns bara
    /// koden — så en adress som aldrig loggat in lämnar inget konto efter sig.
    /// </para>
    /// </summary>
    public async Task<SessionTokens?> VerifyAsync(
        string emailAddress,
        string code,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(emailAddress) || string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var normalized = Normalize(emailAddress);
        var now = clock.GetUtcNow().UtcDateTime;

        var stored = await codes.FindLatestAsync(normalized, cancellationToken).ConfigureAwait(false);

        if (stored is null || !stored.IsUsable(now, Options.MaxLoginCodeAttempts))
        {
            return null;
        }

        if (!CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(stored.CodeHash),
                System.Text.Encoding.UTF8.GetBytes(Hash(code.Trim()))))
        {
            /*
             * Jamforelsen ar tidskonstant. Bade hasharna ar lika langa, sa metoden ar
             * saker att anvanda -- och en vanlig strangjamforelse hade avbrutit vid forsta
             * skiljande tecknet, vilket i teorin gar att mata.
             */
            stored.FailedAttempts++;
            await codes.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return null;
        }

        stored.ConsumedUtc = now;

        var account = await accounts.FindByEmailAsync(normalized, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            account = new Account
            {
                Id = Guid.NewGuid(),
                Email = normalized,
                CreatedUtc = now,
            };

            await accounts.AddAsync(account, cancellationToken).ConfigureAwait(false);
        }

        account.LastSignedInUtc = now;

        await codes.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return await sessions.IssueAsync(account, cancellationToken).ConfigureAwait(false);
    }
}
