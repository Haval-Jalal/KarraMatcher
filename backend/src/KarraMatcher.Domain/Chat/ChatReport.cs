namespace KarraMatcher.Domain.Chat;

/// <summary>
/// En anmälan av ett chattmeddelande (§KM.10, `#201`/`#263`).
///
/// <para>
/// Vilken medlem som helst kan anmäla ett meddelande. Anmälan syns för truppens admin, som
/// kan radera meddelandet först när flera anmälningar kommit in (`#263`). En <b>obligatorisk
/// motivering</b> följer med anmälan (`#263`): den är fritext och därmed potentiell PII
/// (§KM.10) — den loggas aldrig, visas bara för truppens admin, och försvinner med
/// meddelandet när chatten gallras (kaskad/RemoveRange, 90 dagar).
/// </para>
/// </summary>
public sealed class ChatReport
{
    /// <summary>Taket på en motivering. Prövas server-side, inte bara i formuläret.</summary>
    public const int MaxReason = 500;

    public Guid Id { get; set; }

    /// <summary>Meddelandet som anmäldes.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Kontot som anmälde. En vuxen medlem.</summary>
    public Guid ReportedByAccountId { get; set; }

    /// <summary>Varför meddelandet anmäldes. Fritext (§KM.10), obligatorisk (`#263`).</summary>
    public string Reason { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}
