using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Application.Abstractions.Push;

/// <summary>Vad ett försök att skicka en notis kan sluta med.</summary>
public enum PushOutcome
{
    /// <summary>Push-tjänsten tog emot notisen.</summary>
    Delivered,

    /// <summary>
    /// Prenumerationen finns inte längre — <c>404</c> eller <c>410</c> från push-tjänsten.
    ///
    /// <para>
    /// Webbläsaren är avinstallerad, lagringen rensad, eller notiser avstängda. Raden ska
    /// bort: att försöka igen i all evighet kostar anrop och håller kvar en personuppgift
    /// för en enhet som inte finns (§KM.10).
    /// </para>
    /// </summary>
    Gone,

    /// <summary>
    /// Gick inte nu, men kan gå sen — nätverksfel, timeout, eller <c>5xx</c>.
    /// </summary>
    Retry,

    /// <summary>
    /// Gick inte, och kommer inte att gå. Ett fel i vårt anrop, inte i deras tjänst.
    /// </summary>
    Failed,
}

/// <summary>Skickar en enskild notis till en enskild prenumeration.</summary>
public interface IPushSender
{
    public Task<PushOutcome> SendAsync(
        PushTarget target,
        PushMessage message,
        CancellationToken cancellationToken);
}

/// <summary>
/// En enhet att skicka till.
///
/// <para>
/// Adressen är en personuppgift (§KM.10). Den lämnar repositoryt bara hit, och bara för att
/// utskicket ska kunna göras — den loggas aldrig och returneras aldrig i ett API-svar.
/// </para>
/// </summary>
public sealed record PushTarget(Guid Id, string Endpoint, string P256dh, string Auth);
