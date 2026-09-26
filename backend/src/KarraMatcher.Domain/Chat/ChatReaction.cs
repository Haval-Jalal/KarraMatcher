namespace KarraMatcher.Domain.Chat;

/// <summary>
/// En reaktion på ett chattmeddelande (`#301`): en tumme upp, ett hjärta osv.
///
/// <para>
/// Reaktionen är <b>ingen fritext</b> — bara en av en fast uppsättning emoji (<see cref="Allowed"/>),
/// prövad server-side. Därför är den inte potentiell PII som meddelandetexten (§KM.1/§KM.10) och
/// kan aldrig missbrukas för att skriva något. Ett konto reagerar med en viss emoji en gång
/// (unikt index), så en tryckning växlar reaktionen av och på.
/// </para>
///
/// <para>
/// Kaskad från både meddelandet och kontot (§KM.6): raderas meddelandet eller kontot försvinner
/// reaktionen med det.
/// </para>
/// </summary>
public sealed class ChatReaction
{
    /// <summary>
    /// De tillåtna reaktionerna — de vanligaste. En emoji utanför listan avvisas server-side.
    /// Ordningen är den som visas i gränssnittet.
    /// </summary>
    public static readonly IReadOnlyList<string> Allowed = ["👍", "❤️", "😂", "😮", "😢", "👏"];

    /// <summary>Taket på en emoji-sträng i databasen. Rymligt nog för en sammansatt emoji.</summary>
    public const int MaxEmoji = 16;

    public Guid Id { get; set; }

    /// <summary>Meddelandet reaktionen gäller.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Kontot som reagerade. En vuxen medlem.</summary>
    public Guid ReactedByAccountId { get; set; }

    /// <summary>Vilken reaktion. Alltid en av <see cref="Allowed"/>.</summary>
    public string Emoji { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; }
}
