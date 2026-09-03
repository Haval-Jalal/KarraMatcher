namespace KarraMatcher.Domain.Carpool;

/// <summary>
/// En förälder erbjuder skjuts till en match.
///
/// <para>
/// Appens enda funktion där föräldrar gör något med varandra, och därför den mest reglerade
/// (§KM.12).
/// </para>
///
/// <h3>Vad som medvetet inte finns här</h3>
///
/// <para>
/// <b>Inget namn och inget telefonnummer.</b> Kontot lagrar bara en mejladress, och den
/// visas aldrig. Överenskommelsen sker i meddelandefältet — väljer en förälder att själv
/// skriva sitt nummer i notisen är det deras beslut, appen frågar aldrig efter det.
/// </para>
///
/// <para>
/// <b>Ingen räknare för lediga platser.</b> Bara accepterade förfrågningar förbrukar
/// platser, och de räknas där de finns. En cachad siffra här hade blivit fel utan att
/// någon märkte det.
/// </para>
/// </summary>
public sealed class CarpoolOffer
{
    public Guid Id { get; set; }

    public Guid MatchId { get; set; }

    /// <summary>Kontot som lade upp erbjudandet. Bara det får dra tillbaka det.</summary>
    public Guid DriverAccountId { get; set; }

    public CarpoolDirection Direction { get; set; }

    /// <summary>Var bilen går ifrån, som föraren skrivit det. Till exempel "Kärra centrum".</summary>
    public required string DeparturePlace { get; set; }

    /// <summary>Avgång i UTC. Visas i svensk tid, aldrig lagrad så (§KM.5).</summary>
    public DateTime DepartureUtc { get; set; }

    /// <summary>
    /// Lediga platser, 1–4.
    ///
    /// <para>
    /// Fyra är taket därför att det är vad som får plats i en vanlig bil utöver föraren och
    /// det egna barnet. Gränsen prövas server-side, inte bara i formuläret.
    /// </para>
    /// </summary>
    public int Seats { get; set; }

    /// <summary>
    /// Förarens egna ord. Potentiell PII — loggas aldrig, och lämnas ute ur publika svar
    /// (§KM.12).
    /// </summary>
    public string? Note { get; set; }

    public CarpoolOfferStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
