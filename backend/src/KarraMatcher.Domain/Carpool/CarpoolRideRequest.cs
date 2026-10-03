namespace KarraMatcher.Domain.Carpool;

/// <summary>
/// En förälder ber själv om skjuts till en match (`#63`, §KM.12).
///
/// <h3>Spegelbilden av erbjudandet</h3>
///
/// <para>
/// <see cref="CarpoolOffer"/> är förarens sida: "jag kan köra". Den här är passagerarens:
/// "jag behöver skjuts". En förare som ser den kan erbjuda plats (<see cref="CarpoolRideOffer"/>),
/// och den som frågade tackar ja eller nej — precis som föraren svarar på en förfrågan om plats,
/// fast åt andra hållet.
/// </para>
///
/// <h3>Fritexten är lagsynlig, som erbjudandets notis</h3>
///
/// <para>
/// <see cref="Note"/> är en valfri rad ("behöver från Kärra centrum") och visas för lagets
/// medlemmar, precis som ett erbjudandes notis. De tätare orden — förarens platserbjudande och
/// förälderns svar — är det som bara når de två inblandade (§KM.12), och de bor på
/// <see cref="CarpoolRideOffer"/>.
/// </para>
/// </summary>
public sealed class CarpoolRideRequest
{
    public Guid Id { get; set; }

    /// <summary>Händelsen (matchen) förfrågan gäller. Samåkning finns bara på matcher (§KM.12).</summary>
    public Guid MatchId { get; set; }

    /// <summary>Kontot som bad om skjuts. Bara det får dra tillbaka den, eller svara på platserbjudanden.</summary>
    public Guid RequesterAccountId { get; set; }

    /// <summary>Vilken väg skjutsen behövs.</summary>
    public CarpoolDirection Direction { get; set; }

    /// <summary>Antal platser som behövs. Standard 1.</summary>
    public int Seats { get; set; }

    /// <summary>
    /// Valfri rad till laget ("behöver från Kärra centrum"). Potentiell PII — loggas aldrig
    /// (§KM.10), men lagsynlig som ett erbjudandes notis.
    /// </summary>
    public string? Note { get; set; }

    public CarpoolRideRequestStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Sant så länge förfrågan fortfarande söker skjuts. En löst eller tillbakadragen förfrågan
    /// tar inte emot fler platserbjudanden.
    /// </summary>
    public bool IsOpen => Status == CarpoolRideRequestStatus.Open;
}
