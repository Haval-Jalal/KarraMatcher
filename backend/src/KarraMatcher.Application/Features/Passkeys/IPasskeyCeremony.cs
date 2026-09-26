namespace KarraMatcher.Application.Features.Passkeys;

/// <summary>
/// De två WebAuthn-ceremonierna — registrera en passkey och logga in med en.
///
/// <para>
/// Interfacet ligger i Application men implementeras i Infrastructure, där WebAuthn-biblioteket
/// bor. Protokoll-objekten (utmaningar, credential-svar) passerar som JSON-strängar, så
/// applikationslagret slipper känna till bibliotekets typer — samma gräns som mot alla andra
/// ramverksberoenden.
/// </para>
///
/// <para>
/// Ceremonierna är tvåstegs: <c>Begin</c> skapar en utmaning som lagras kortlivat server-side,
/// <c>Complete</c> hämtar den och verifierar svaret mot den. En utmaning är giltig en kort stund
/// och bara en gång.
/// </para>
/// </summary>
public interface IPasskeyCeremony
{
    /// <summary>Startar registrering för ett inloggat konto. Returnerar WebAuthn-options som JSON.</summary>
    public Task<string> BeginRegistrationAsync(Guid accountId, CancellationToken cancellationToken);

    /// <summary>Verifierar registrerings-svaret och sparar passkey:n på kontot.</summary>
    public Task<PasskeyRegistrationOutcome> CompleteRegistrationAsync(
        Guid accountId,
        string attestationJson,
        string? deviceLabel,
        CancellationToken cancellationToken);

    /// <summary>Startar inloggning (usernameless). Returnerar utmaningens id och options som JSON.</summary>
    public Task<PasskeyLoginChallenge> BeginLoginAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Verifierar inloggnings-svaret mot den lagrade utmaningen och den sparade publika nyckeln.
    /// Returnerar kontots id vid framgång, annars null.
    /// </summary>
    public Task<Guid?> CompleteLoginAsync(
        string challengeId,
        string assertionJson,
        CancellationToken cancellationToken);
}

/// <summary>Utfall av en passkey-registrering.</summary>
public enum PasskeyRegistrationOutcome
{
    Registered = 0,

    /// <summary>Svaret gick inte att verifiera (fel domän, manipulerat, redan registrerat).</summary>
    Invalid = 1,
}

/// <summary>En påbörjad inloggnings-ceremoni: utmaningens id (skickas tillbaka) och options-JSON.</summary>
public sealed record PasskeyLoginChallenge(string ChallengeId, string OptionsJson);

/// <summary>En passkey så som kontot ser den i listan. Ingen nyckel, inget id att missbruka.</summary>
public sealed record PasskeyDto(
    Guid Id,
    string? DeviceLabel,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? LastUsedUtc);
