namespace KarraMatcher.Domain.Carpool;

/// <summary>
/// Erbjudandets tillstånd.
///
/// <para>
/// §KM.12 beskriver kedjan <c>Öppet → Fullt → Tillbakadraget</c>. <b>Fullt lagras inte
/// här</b>, utan räknas fram ur antalet accepterade förfrågningar. Ett sparat "fullt" hade
/// varit ett andra ställe som vet hur många platser som är kvar, och det stället hade
/// förr eller senare sagt något annat än förfrågningarna — vilket är precis det fel som
/// gör att någon står kvar på parkeringen.
/// </para>
/// </summary>
public enum CarpoolOfferStatus
{
    /// <summary>Ligger uppe och går att fråga om.</summary>
    Open = 0,

    /// <summary>Föraren har dragit tillbaka det. Syns inte längre som bokningsbart.</summary>
    Withdrawn = 1,
}
