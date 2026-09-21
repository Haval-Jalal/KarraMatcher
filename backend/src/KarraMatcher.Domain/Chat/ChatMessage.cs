namespace KarraMatcher.Domain.Chat;

/// <summary>
/// Ett meddelande i en chatt (§KM.1/§KM.10, `#201`).
///
/// <h3>Kanal: trupp nu, lag sedan</h3>
///
/// <para>
/// Meddelandet hör alltid till en trupp (<see cref="AgeGroupId"/>). <see cref="TeamId"/> är
/// null för trupp-chatten; `#202` sätter ett lag för lag-kanalerna ovanpå samma modell. En
/// kanal är alltså paret (trupp, ev. lag).
/// </para>
///
/// <h3>Fritext är potentiell PII</h3>
///
/// <para>
/// <see cref="Body"/> är det en förälder skrivit — den loggas aldrig (§KM.10), gallras
/// (`#201`), och vid moderering töms den direkt medan raden blir kvar som en "[borttaget]"-
/// markering. Vi skriver aldrig själva ett barns namn i en chatt (§KM.1).
/// </para>
///
/// <h3>Schemalagt utskick</h3>
///
/// <para>
/// <see cref="PublishAtUtc"/> är när meddelandet ska bli synligt. För ett vanligt meddelande
/// är det nu, och <see cref="PublishedUtc"/> sätts direkt. En admin eller tränare kan i
/// stället välja en framtida tid — då är <see cref="PublishedUtc"/> null tills ett
/// bakgrundsjobb släpper det, och meddelandet syns inte och ger ingen notis dessförinnan.
/// </para>
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; set; }

    /// <summary>Truppen (åldersgruppen) meddelandet hör till. Alltid satt.</summary>
    public Guid AgeGroupId { get; set; }

    /// <summary>Lag-kanalen, eller null för trupp-chatten (`#202` sätter lag).</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Kontot som skrev. En vuxen medlem.</summary>
    public Guid AuthorAccountId { get; set; }

    /// <summary>Meddelandetexten. Potentiell PII — loggas aldrig, gallras, töms vid radering.</summary>
    public string Body { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }

    /// <summary>När meddelandet ska bli synligt. Nu för ett vanligt meddelande, framtid för schemalagt.</summary>
    public DateTime PublishAtUtc { get; set; }

    /// <summary>När meddelandet blev synligt, eller null medan det är schemalagt och väntar.</summary>
    public DateTime? PublishedUtc { get; set; }

    /// <summary>När meddelandet togs bort, eller null. Vid radering töms <see cref="Body"/>.</summary>
    public DateTime? DeletedUtc { get; set; }

    /// <summary>
    /// Kontot som tog bort meddelandet, eller null. Ingen främmande nyckel — som audit-raden
    /// överlever tombstonen att kontot raderas.
    /// </summary>
    public Guid? DeletedByAccountId { get; set; }
}
