namespace KarraMatcher.Domain.Accounts;

/// <summary>
/// Ett konto — en vuxen som loggar in för att lägga upp samåkning eller sköta ett lag.
///
/// <para>
/// <b>Kontot är den enda personuppgiften servern lagrar om en användare, och det räcker
/// med adressen.</b> Inget namn, inget telefonnummer, ingen koppling till ett barn. Ett
/// konto behövs bara för att skriva; att läsa schemat kräver ingenting (§KM.3).
/// </para>
///
/// <para>
/// Spelarkortet hör inte hit och kan inte göra det: barnets statistik lämnar aldrig
/// familjens telefon (§KM.2). Ett konto som raderas tar med sig allt det äger på servern,
/// men rör inte spelarkortet — det ligger kvar i telefonen tills familjen själv tar bort
/// det.
/// </para>
/// </summary>
public sealed class Account
{
    public Guid Id { get; set; }

    /// <summary>
    /// Inloggningsadressen, normaliserad till gemener.
    ///
    /// <para>
    /// Lagras normaliserad därför att en adress är samma adress oavsett skiftläge, och för
    /// att ett unikt index annars hade släppt igenom två konton för samma person. Aldrig i
    /// loggar (§KM.10) — referera till kontot med <see cref="Id"/>.
    /// </para>
    /// </summary>
    public required string Email { get; set; }

    /// <summary>
    /// Förnamnet, som föräldern själv skrivit det.
    ///
    /// <para>
    /// Finns för att samåkningen ska ha ett ansikte: "Anna frågar om skjuts" i stället för
    /// "någon frågar om skjuts" (`#154`). Nullbart därför att konton skapade före
    /// funktionen inte har något — gränssnittet frågar efter det vid nästa inloggning.
    /// </para>
    ///
    /// <para>
    /// Personuppgift om en <b>vuxen</b>, inte om ett barn — §KM.1:s tak gäller barn och
    /// berörs inte. Visas bara för inloggade i laget, aldrig för en gäst, och aldrig i
    /// loggar eller audit-rader (§KM.3, §KM.10).
    /// </para>
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Efternamnet. Valfritt.
    ///
    /// <para>
    /// Valfritt med flit: i ett föräldralag räcker förnamnet nästan alltid, och det som
    /// inte behövs ska inte krävas. Den som vill skilja två Anna åt kan fylla i det.
    /// </para>
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Namnet att visa, eller null när kontot inte fyllt i något.
    ///
    /// <para>
    /// Härlett och inte lagrat: ett sparat visningsnamn är ett andra ställe som vet samma
    /// sak, och det är det som glider isär.
    /// </para>
    /// </summary>
    public string? DisplayName => FirstName is null or ""
        ? null
        : string.IsNullOrWhiteSpace(LastName) ? FirstName : $"{FirstName} {LastName}";

    public DateTime CreatedUtc { get; set; }

    /// <summary>Senaste lyckade inloggning. Används för gallring av vilande konton.</summary>
    public DateTime? LastSignedInUtc { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; } = [];
}
