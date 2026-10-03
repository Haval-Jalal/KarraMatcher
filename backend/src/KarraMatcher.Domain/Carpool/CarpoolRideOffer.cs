namespace KarraMatcher.Domain.Carpool;

/// <summary>
/// En förare erbjuder plats på en <see cref="CarpoolRideRequest"/> (`#63`, §KM.12).
///
/// <h3>Spegelbilden av en åkförfrågan</h3>
///
/// <para>
/// Det här är exakt <see cref="CarpoolRequest"/> fast med rollerna ombytta: där frågar en
/// passagerare om plats på ett erbjudande och föraren svarar; här erbjuder en förare plats på en
/// åkförfrågan och den som frågade svarar. Statuskedjan är densamma och återanvänds
/// (<see cref="CarpoolRequestStatus"/>): Väntar → Accepterad / Nekad, eller Återtagen av föraren.
/// </para>
///
/// <h3>Ägarna av samma rad</h3>
///
/// <para>
/// Platserbjudandet tillhör föraren (<see cref="DriverAccountId"/>) — bara hen får återta det.
/// Svaret (<see cref="ResponseMessage"/> + statusbytet till Accepterad/Nekad) tillhör den som
/// äger åkförfrågan. Ett nekande kräver alltid ett meddelande (§KM.12).
/// </para>
/// </summary>
public sealed class CarpoolRideOffer
{
    public Guid Id { get; set; }

    /// <summary>Åkförfrågan detta platserbjudande gäller.</summary>
    public Guid RideRequestId { get; set; }

    /// <summary>Kontot som erbjuder plats (föraren). Bara det får återta erbjudandet.</summary>
    public Guid DriverAccountId { get; set; }

    /// <summary>Antal platser föraren erbjuder. Standard 1.</summary>
    public int Seats { get; set; }

    /// <summary>
    /// Förarens hälsning till den som frågade. Potentiell PII — loggas aldrig och visas bara för
    /// de två inblandade (§KM.12).
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Förälderns ord tillbaka. Krävs vid ett nekande (§KM.12), valfritt vid en accept. Samma
    /// sorts fritext som hälsningen och behandlas likadant.
    /// </summary>
    public string? ResponseMessage { get; set; }

    /// <summary>Samma kedja som en åkförfrågan (Väntar/Accepterad/Nekad/Återtagen).</summary>
    public CarpoolRequestStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Sant när platserbjudandet fortfarande gör anspråk på något. Avgör om föraren får erbjuda
    /// plats igen: ett nekat eller återtaget erbjudande blockerar inte en nytt försök.
    /// </summary>
    public bool IsActive =>
        Status is CarpoolRequestStatus.Pending or CarpoolRequestStatus.Accepted;
}
