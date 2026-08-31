namespace KarraMatcher.Api.Features.Auth;

/// <summary>
/// Refresh-cookien: sätts, läses och rensas på ett enda ställe.
///
/// <para>
/// <b>Att den kan vara förstapart är hela poängen med Vercel-rewriten</b> (§KM.11).
/// Klienten ser en enda origin, så cookien behöver varken <c>SameSite=None</c> eller en
/// uppluckrad CORS-policy. Blir det någon gång ett CORS-fel här är orsaken att någon
/// anropat Render-adressen direkt — fixa anropet, inte policyn.
/// </para>
/// </summary>
internal static class SessionCookie
{
    public const string Name = "karra_refresh";

    /// <summary>
    /// Cookien skickas bara till inloggningens egna endpoints.
    ///
    /// <para>
    /// Utan sökvägen hade den följt med varje anrop till schemat och kalenderfeeden —
    /// alltså till svar som cachas på Vercels edge. Den vore fortfarande <c>httpOnly</c>,
    /// men det finns ingen anledning att skicka den dit den inte behövs.
    /// </para>
    /// </summary>
    private const string Path = "/api/v1/auth";

    public static string? Read(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Cookies.TryGetValue(Name, out var value) ? value : null;
    }

    public static void Write(HttpResponse response, string token, DateTime expiresUtc)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(Name, token, Options(expiresUtc));
    }

    /// <summary>
    /// Tar bort cookien genom att skriva över den med en som redan gått ut.
    ///
    /// <para>
    /// Attributen måste vara identiska med dem den sattes med, annars raderar webbläsaren
    /// ingenting och användaren är kvar med en cookie som servern redan återkallat.
    /// </para>
    /// </summary>
    public static void Clear(HttpResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        response.Cookies.Append(Name, string.Empty, Options(DateTime.UnixEpoch));
    }

    private static CookieOptions Options(DateTime expiresUtc) => new()
    {
        // Oåtkomlig för JavaScript. Det som skiljer en stulen token från ett XSS-fönster.
        HttpOnly = true,

        // Aldrig över klartext. Sätts alltid, även lokalt — dev-servern kör https.
        Secure = true,

        // Lax och inte Strict: en förälder som klickar på en länk i föräldragruppen ska
        // landa inloggad. Strict hade tvingat fram en extra sidladdning för att cookien
        // skulle följa med, vilket ser ut som att inloggningen tappats bort.
        SameSite = SameSiteMode.Lax,

        Path = Path,
        Expires = new DateTimeOffset(expiresUtc, TimeSpan.Zero),
        IsEssential = true,
    };
}
