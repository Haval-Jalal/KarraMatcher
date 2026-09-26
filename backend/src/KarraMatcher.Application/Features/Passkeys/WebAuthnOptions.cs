namespace KarraMatcher.Application.Features.Passkeys;

/// <summary>
/// Var passkeys hör hemma (WebAuthn "relying party"). Sätts per miljö — domänen avgör vilka
/// nycklar webbläsaren släpper fram, så den måste stämma med var appen faktiskt körs.
/// </summary>
public sealed class WebAuthnOptions
{
    public const string SectionName = "WebAuthn";

    /// <summary>Domänen passkeys binds till, t.ex. "karramatcher.se". Lokalt "localhost".</summary>
    public string RelyingPartyId { get; set; } = "localhost";

    /// <summary>Namnet som visas i enhetens passkey-dialog.</summary>
    public string ServerName { get; set; } = "Kärra Matcher";

    /// <summary>Tillåtna ursprung (origin), t.ex. "https://karramatcher.se". Lokalt vite-porten.</summary>
    public IList<string> Origins { get; set; } = new List<string> { "http://localhost:5173" };
}
