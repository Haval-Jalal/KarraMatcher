using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Prenumerationer på ett lags notiser (`#60`).
///
/// <para>
/// Interfacet lämnar aldrig ut en push-adress. Den är en personuppgift (§KM.10) och
/// behövs bara av utskicket, som byggs i <c>#61</c> och då får en egen läsväg. Att den
/// inte går att läsa härifrån är ett medvetet val, inte en lucka.
/// </para>
/// </summary>
public interface IPushSubscriptionRepository
{
    /// <summary>
    /// Registrerar (eller uppdaterar) en enhets prenumeration (`#332`-uppföljning).
    /// </summary>
    /// <remarks>
    /// Per <b>enhet</b>, inte per lag: högst en rad per webbläsare (upsert på adressen). En
    /// förälder som slår på notiser för flera lag — eller laddar om sidan — får inte längre en
    /// rad per lag och därmed flera identiska notiser. Utskicket väljer mottagare på medlemskap.
    /// </remarks>
    public Task SubscribeAsync(
        PushSubscriptionDraft draft,
        Guid accountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tar bort prenumerationen för en adress. Falskt när det inte fanns någon.
    /// </summary>
    /// <remarks>
    /// Anroparen får samma svar oavsett — att avregistrera något som inte finns är inte ett
    /// fel, och skillnaden hade avslöjat om en adress är känd hos oss.
    /// </remarks>
    public Task<bool> UnsubscribeAsync(string endpoint, CancellationToken cancellationToken);
}
