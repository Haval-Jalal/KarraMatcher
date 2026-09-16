namespace KarraMatcher.Application.Features.Auth;

/// <summary>
/// Vad ett konto får göra, i den form som ska in i en token.
///
/// <para>
/// Lagen anges med <b>slug</b> och inte med id. Adresserna i appen är byggda på slug
/// (<c>/lag/gul</c>), så en behörighetskontroll mot ett lag i en route blir en jämförelse
/// av två strängar — utan en databasfråga i varje anrop.
/// </para>
///
/// <para>
/// Priset är att en omdöpt slug gör en tränares behörighet ogiltig tills token förnyas.
/// Slugen är avsedd att vara stabil — den ligger i länkar föräldrar delar med varandra —
/// så det är ett byte vi kan leva med.
/// </para>
/// </summary>
public sealed record AccountRoles(
    bool IsSuperAdmin,
    IReadOnlyList<string> AdminOf,
    IReadOnlyList<string> CoachOf)
{
    public static readonly AccountRoles None = new(false, [], []);

    /// <summary>
    /// Bakåtkompatibel global admin (v2): superadmin är den globala administratören tills
    /// den per-trupp-baserade auktoriseringen kopplas in i `#191`. Låter befintliga
    /// <c>[Authorize(Admin)]</c>-endpoints fortsätta gälla superadmin.
    /// </summary>
    public bool IsAdmin => IsSuperAdmin;
}
