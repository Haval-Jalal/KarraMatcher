namespace KarraMatcher.Domain.Chat;

/// <summary>
/// En anmälan av ett chattmeddelande (§KM.10, `#201`).
///
/// <para>
/// Vilken medlem som helst kan anmäla ett meddelande. Anmälan syns för truppens admin, som
/// kan radera meddelandet. Ingen fritext här — bara vem som anmälde vilket meddelande och
/// när; ett skäl i klartext hade varit ännu en fritext att gallra och skydda (§KM.10).
/// </para>
/// </summary>
public sealed class ChatReport
{
    public Guid Id { get; set; }

    /// <summary>Meddelandet som anmäldes.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Kontot som anmälde. En vuxen medlem.</summary>
    public Guid ReportedByAccountId { get; set; }

    public DateTime CreatedUtc { get; set; }
}
