namespace KarraMatcher.Domain.Accounts;

/// <summary>
/// En passkey (WebAuthn-credential) för ett konto — inloggning utan lösenord eller kod.
///
/// <h3>Nyckeln bevisar, servern lagrar bara den publika halvan</h3>
///
/// <para>
/// Den privata nyckeln lämnar aldrig enhetens säkra hårdvara (bakom Face ID/fingeravtryck).
/// Här lagras bara den <em>publika</em> nyckeln och credential-id:t: nog för att verifiera att
/// enheten har den privata, aldrig nog för att låtsas vara den. Inget här är en personuppgift om
/// ett barn (§KM.1) — en passkey hör till ett vuxet konto och försvinner med det (§KM.6).
/// </para>
///
/// <h3>Räknaren skyddar mot kloning</h3>
///
/// <para>
/// <see cref="SignCount"/> ökar för varje inloggning; går den bakåt är credential:et klonat och
/// inloggningen avvisas. Den lagras som <c>long</c> (WebAuthn:s räknare är 32-bitars osignerad).
/// </para>
/// </summary>
public sealed class Passkey
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    /// <summary>Credential-id:t webbläsaren skickar vid inloggning. Unikt.</summary>
    public required byte[] CredentialId { get; set; }

    /// <summary>Den publika nyckeln, för att verifiera signaturen vid inloggning.</summary>
    public required byte[] PublicKey { get; set; }

    /// <summary>Signeringsräknaren (WebAuthn 32-bitars osignerad, lagrad som long).</summary>
    public long SignCount { get; set; }

    /// <summary>Ett vänligt namn på enheten, t.ex. "iPhone". Valfritt, ingen PII.</summary>
    public string? DeviceLabel { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? LastUsedUtc { get; set; }
}
