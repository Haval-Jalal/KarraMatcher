namespace KarraMatcher.Application.Features.Auth;

/// <summary>
/// Namnen på anspråken i en access-token.
///
/// <para>
/// Ligger här och inte hos utfärdaren, eftersom de är ett kontrakt mellan två sidor: den
/// som skriver token och den som läser den. Med namnen på ett ställe kan de inte glida
/// isär — och en stavskillnad mellan skrivning och läsning är ett behörighetsfel som inte
/// syns någonstans, den ger bara ett anspråk som aldrig matchar.
/// </para>
/// </summary>
public static class AuthClaims
{
    /// <summary>Ett anspråk per lag en tränare ansvarar för. Värdet är lagets slug.</summary>
    public const string Coach = "coach";

    /// <summary>Värdet i standardanspråket för roll när kontot är administratör.</summary>
    public const string AdminRole = "admin";
}
