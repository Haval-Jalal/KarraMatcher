namespace KarraMatcher.Domain.Accounts;

/// <summary>
/// En refresh-token i en kedja av utfärdade tokens.
///
/// <para>
/// Sessionerna i en PWA är långlivade, så en stulen token är värdefull länge. Rotation med
/// återanvändningsdetektering är det som gör den kortlivad i praktiken: varje förnyelse
/// utfärdar en ny token och märker den gamla som ersatt. Dyker en redan ersatt token upp
/// igen finns den på två ställen — någon har kopierat den — och då återkallas
/// <b>hela familjen</b>, inte bara den token som användes (checklistan 1.5).
/// </para>
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    /// <summary>
    /// SHA-256 av tokenvärdet. <b>Klartexten lagras aldrig.</b>
    ///
    /// <para>
    /// En läckt databas ska inte vara en hink med giltiga sessioner. Samma resonemang som
    /// för lösenord, och det kostar oss ingenting: vi behöver aldrig läsa tokenvärdet, bara
    /// känna igen det när det kommer tillbaka.
    /// </para>
    /// </summary>
    public required string TokenHash { get; set; }

    /// <summary>
    /// Kedjan den här token tillhör. Sätts vid inloggning och ärvs vid varje rotation.
    ///
    /// <para>
    /// Familjen är det som gör återkallandet meningsfullt. Utan den hade en upptäckt
    /// stöld bara ogiltigförklarat den token tjuven redan använt — och tjuven hade haft
    /// nästa i kedjan.
    /// </para>
    /// </summary>
    public Guid FamilyId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    /// <summary>Satt när token bytts mot en ny. En ersatt token får aldrig användas igen.</summary>
    public DateTime? ReplacedUtc { get; set; }

    /// <summary>
    /// Satt när token återkallats — vid utloggning, kontoradering, eller för att någon
    /// annan i familjen återanvänts.
    /// </summary>
    public DateTime? RevokedUtc { get; set; }

    /// <summary>Sann bara för en token som varken bytts, återkallats eller gått ut.</summary>
    public bool IsActive(DateTime nowUtc) =>
        ReplacedUtc is null && RevokedUtc is null && ExpiresUtc > nowUtc;
}
