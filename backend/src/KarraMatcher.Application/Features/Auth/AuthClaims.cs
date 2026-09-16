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

    /// <summary>Ett anspråk per trupp en admin ansvarar för. Värdet är truppens id (v2).</summary>
    public const string AdminOfTrupp = "admin-trupp";

    /// <summary>Sätts (till <c>true</c>) när kontot är superadmin — global ägare (v2).</summary>
    public const string SuperAdmin = "superadmin";

    /// <summary>
    /// Värdet i standardanspråket för roll när kontot är (super)administratör. Behålls för
    /// bakåtkompatibilitet med befintliga <c>[Authorize(Admin)]</c>-endpoints (§KM.3, v2).
    /// </summary>
    public const string AdminRole = "admin";
}
