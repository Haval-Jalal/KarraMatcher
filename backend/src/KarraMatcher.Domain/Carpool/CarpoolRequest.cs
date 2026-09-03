namespace KarraMatcher.Domain.Carpool;

/// <summary>
/// En förälder frågar om att få åka med.
///
/// <h3>Föraren väljer, det är inte först till kvarn</h3>
///
/// <para>
/// Därför är det här en <em>förfrågan</em> och inte en bokning. Den som frågar tar ingen
/// plats i anspråk; det gör först förarens accept (§KM.12). Det är den sociala verkligheten
/// i ett föräldralag — man möts på planen nästa lördag.
/// </para>
///
/// <h3>Går att skicka även när erbjudandet är fullt</h3>
///
/// <para>
/// Avsiktligt. Föraren ska kunna svara "någon annan hann före" i stället för att den som
/// frågar möts av en död knapp.
/// </para>
/// </summary>
public sealed class CarpoolRequest
{
    public Guid Id { get; set; }

    public Guid OfferId { get; set; }

    /// <summary>Kontot som frågade. Bara det får återta förfrågan.</summary>
    public Guid RequesterAccountId { get; set; }

    /// <summary>Antal platser som efterfrågas. Standard 1.</summary>
    public int Seats { get; set; }

    /// <summary>
    /// Hälsningen till föraren. Potentiell PII — loggas aldrig och visas bara för de
    /// inblandade och lagets tränare (§KM.12).
    /// </summary>
    public string? Message { get; set; }

    public CarpoolRequestStatus Status { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// Sant när förfrågan fortfarande gör anspråk på något.
    ///
    /// <para>
    /// Det är den här definitionen som avgör om en ny förfrågan får skickas. En nekad eller
    /// återtagen förfrågan blockerar inte — planerna kan ha ändrats, och att låsa någon ute
    /// för att de frågat en gång vore fel. En som väntar eller är accepterad gör det: att
    /// fråga igen om samma sak är brus för föraren.
    /// </para>
    /// </summary>
    public bool IsActive =>
        Status is CarpoolRequestStatus.Pending or CarpoolRequestStatus.Accepted;
}
