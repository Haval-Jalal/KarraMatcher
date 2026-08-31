namespace KarraMatcher.Domain.Accounts;

/// <summary>
/// En engångskod som skickats till en adress.
///
/// <para>
/// Knuten till <see cref="Email"/> och inte till ett konto, eftersom kontot kanske inte
/// finns än — det skapas först när en kod faktiskt verifierats. Att skapa kontot redan
/// vid begäran hade låtit vem som helst fylla tabellen med adresser de hittat på.
/// </para>
///
/// <para>
/// Koden lagras hashad av samma skäl som refresh-tokens: en läckt databas ska inte
/// innehålla giltiga inloggningar. Här är hashen dessutom vad verifieringen jämför mot,
/// så klartexten behövs aldrig efter att mejlet skickats.
/// </para>
/// </summary>
public sealed class LoginCode
{
    public Guid Id { get; set; }

    /// <summary>Adressen koden skickades till, normaliserad till gemener.</summary>
    public required string Email { get; set; }

    /// <summary>SHA-256 av koden. Klartexten lämnar servern en gång, i mejlet.</summary>
    public required string CodeHash { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    /// <summary>Satt när koden använts. En kod fungerar exakt en gång.</summary>
    public DateTime? ConsumedUtc { get; set; }

    /// <summary>
    /// Antal felaktiga försök mot just den här koden.
    ///
    /// <para>
    /// Räknaren är det som gör en sexsiffrig kod försvarbar. Utan den vore en miljon
    /// möjliga koder inget skydd alls — med den räcker gissningarna inte till.
    /// </para>
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>Sann bara för en kod som varken använts, gått ut eller gissats sönder.</summary>
    public bool IsUsable(DateTime nowUtc, int maxAttempts) =>
        ConsumedUtc is null && ExpiresUtc > nowUtc && FailedAttempts < maxAttempts;
}
