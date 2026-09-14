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
    /// Registrerar en prenumeration. Falskt när laget inte finns.
    /// </summary>
    /// <remarks>
    /// Idempotent: samma webbläsare och samma lag ger samma rad. En förälder som laddar om
    /// sidan ska inte få två notiser för samma flyttade match.
    /// </remarks>
    public Task<bool> SubscribeAsync(
        string slug,
        PushSubscriptionDraft draft,
        Guid? accountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Tar bort prenumerationen. Falskt när det inte fanns någon.
    /// </summary>
    /// <remarks>
    /// Anroparen får samma svar oavsett — att avregistrera något som inte finns är inte ett
    /// fel, och skillnaden hade avslöjat om en adress är känd hos oss.
    /// </remarks>
    public Task<bool> UnsubscribeAsync(
        string slug,
        string endpoint,
        CancellationToken cancellationToken);
}
