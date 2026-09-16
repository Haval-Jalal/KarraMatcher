namespace KarraMatcher.Domain.Consent;

/// <summary>
/// En vårdnadshavares samtycke till att en minimal barnprofil lagras på servern (§KM.6, `#195`).
///
/// <para>
/// <b>Per vårdnadshavare, inte per barn.</b> En vuxen samtycker en gång till en bestämd
/// version av samtyckestexten; det låser upp koppling av deras barn (`#196`). Ändras texten
/// bumpas versionen och ett nytt samtycke krävs — därför sparas <see cref="Version"/> och
/// <see cref="GrantedUtc"/>, så det alltid går att svara på <em>vad</em> någon samtyckte till
/// och <em>när</em>.
/// </para>
///
/// <para>
/// Raderas kontot försvinner samtycket med det (§KM.6). Posten bär inget om barnet — bara
/// vem den vuxna är (som id), vilken version och när.
/// </para>
/// </summary>
public sealed class GuardianConsent
{
    public Guid Id { get; set; }

    /// <summary>Vårdnadshavaren som samtyckte.</summary>
    public Guid AccountId { get; set; }

    public Accounts.Account? Account { get; set; }

    /// <summary>Vilken version av samtyckestexten som godkändes.</summary>
    public required string Version { get; set; }

    public DateTime GrantedUtc { get; set; }
}
