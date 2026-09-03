namespace KarraMatcher.Domain.Carpool;

/// <summary>
/// Förfrågans tillstånd, precis den kedja §KM.12 beskriver.
///
/// <code>
/// Väntar ──▶ Accepterad
///        └─▶ Nekad
///        └─▶ Återtagen (av den som frågade)
/// </code>
///
/// <para>
/// Alla fyra finns här fastän bara två nås i dag: <see cref="Accepted"/> och
/// <see cref="Denied"/> är förarens svar och byggs i <c>#52</c>. De står med därför att
/// kedjan är <em>specificerad</em>, inte gissad — och en enum som växer i efterhand får
/// lätt värden i en ordning som inte betyder något.
/// </para>
/// </summary>
public enum CarpoolRequestStatus
{
    /// <summary>Skickad, väntar på förarens svar.</summary>
    Pending = 0,

    /// <summary>Föraren har sagt ja. Först nu förbrukas platser.</summary>
    Accepted = 1,

    /// <summary>Föraren har sagt nej — alltid med ett meddelande (§KM.12).</summary>
    Denied = 2,

    /// <summary>Den som frågade har återtagit den.</summary>
    Retracted = 3,
}
