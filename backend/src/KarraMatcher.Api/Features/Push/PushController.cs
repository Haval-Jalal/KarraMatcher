using System.Security.Claims;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Push;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Push;

/// <summary>
/// Prenumerationer på ett lags notiser (`#60`).
///
/// <h3>Stängd i v2 (§KM.3, `#191`)</h3>
///
/// <para>
/// Notiser är inte längre öppna. Man ser lagets information — matcher, träningar,
/// aviseringar — först som medlem, och då knyts prenumerationen till kontot. Att bara
/// medlemmar kan prenumerera betyder också att en push-adress aldrig registreras av någon
/// utanför laget.
/// </para>
///
/// <h3>Vad som aldrig kommer tillbaka</h3>
///
/// <para>
/// Inget svar bär en push-adress, inte ens till den som just skickade in den. Adressen
/// identifierar en enskild enhet lika bra som ett telefonnummer (§KM.10). Svaren är
/// därför tomma med flit, och lika oavsett om adressen var känd sedan tidigare.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1")]
[Produces("application/json")]
[Authorize]
public sealed class PushController(
    ICommandDispatcher commands,
    IOptions<PushOptions> options) : ControllerBase
{
    /// <summary>Den publika VAPID-nyckeln, som webbläsaren behöver för att prenumerera.</summary>
    /// <remarks>
    /// Serveras av API:t i stället för att bakas in i frontendens bygge, så att ett
    /// nyckelbyte inte kräver en ny driftsättning av frontenden — och så att de två aldrig
    /// kan glida isär. Saknas nycklarna är push avstängt, och då finns ingen nyckel att ge.
    /// </remarks>
    [HttpGet("push/key")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult PublicKey()
    {
        var push = options.Value;

        return push.IsConfigured
            ? Ok(new PushKeyResponse(push.PublicKey))
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Notiser är inte påslagna",
                detail: "Kalendern och schemat fungerar som vanligt.");
    }

    /// <summary>
    /// Slår på notiser på den här enheten (`#332`-uppföljning). En rad per webbläsare, knuten till
    /// kontot — inte per lag. Utskicket väljer mottagare på medlemskap, så en enhet får en notis.
    /// </summary>
    [HttpPost("push")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Subscribe(
        PushSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        await commands
            .SendAsync(new SubscribeToPushCommand(request.ToDraft(), actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    /// <summary>Slutar prenumerera på den här enheten.</summary>
    /// <remarks>
    /// Svarar 204 även när ingen prenumeration fanns. Att avregistrera något som inte finns
    /// är inte ett fel — och ett annat svar hade avslöjat om en adress är känd hos oss.
    /// </remarks>
    [HttpDelete("push")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Unsubscribe(
        PushUnsubscribeRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        await commands
            .SendAsync(
                new UnsubscribeFromPushCommand(request.Endpoint, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }

    private ObjectResult Unauthenticated() => Problem(
        statusCode: StatusCodes.Status401Unauthorized,
        title: "Sessionen gäller inte längre",
        detail: "Logga in igen.");

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>Den publika nyckeln, base64url — den enda av de två som får lämna servern.</summary>
public sealed record PushKeyResponse(string PublicKey);

/// <summary>Det webbläsaren lämnar ifrån sig när användaren tillåter notiser.</summary>
public sealed record PushSubscriptionRequest(string Endpoint, string P256dh, string Auth)
{
    internal PushSubscriptionDraft ToDraft() => new(Endpoint, P256dh, Auth);
}

/// <summary>Adressen räcker för att sluta — det är den enda nyckel webbläsaren har.</summary>
public sealed record PushUnsubscribeRequest(string Endpoint);
