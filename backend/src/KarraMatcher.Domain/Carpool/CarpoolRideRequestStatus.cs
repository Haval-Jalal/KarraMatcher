namespace KarraMatcher.Domain.Carpool;

/// <summary>
/// En skjutsförfrågans tillstånd — när en förälder själv ber om skjuts (`#63`, §KM.12).
///
/// <code>
/// Öppen ──▶ Löst (en förare erbjöd plats och föräldern tackade ja)
///       └─▶ Tillbakadragen (av den som frågade)
/// </code>
///
/// <para>
/// Spegelbilden av erbjudandet: där erbjuder en förare och föräldrar frågar om plats; här ber
/// en förälder om skjuts och förare erbjuder plats. "Löst" lagras — till skillnad från
/// erbjudandets "fullt" är det inte en räkning utan ett faktum: någon kör.
/// </para>
/// </summary>
public enum CarpoolRideRequestStatus
{
    /// <summary>Ligger uppe — förare kan erbjuda plats.</summary>
    Open = 0,

    /// <summary>En förares platserbjudande har accepterats. Någon kör.</summary>
    Fulfilled = 1,

    /// <summary>Den som frågade har dragit tillbaka den.</summary>
    Withdrawn = 2,
}
