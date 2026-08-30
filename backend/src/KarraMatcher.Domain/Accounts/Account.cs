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

    public DateTime CreatedUtc { get; set; }

    /// <summary>Senaste lyckade inloggning. Används för gallring av vilande konton.</summary>
    public DateTime? LastSignedInUtc { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; } = [];
}
