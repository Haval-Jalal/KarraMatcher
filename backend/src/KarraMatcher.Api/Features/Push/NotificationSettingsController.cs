using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Push;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Push;

/// <summary>
/// Kontots globala notisinställning — en enda på/av (`#332`-uppföljning, ersätter per-lag/per-typ
/// `#65`).
///
/// <para>
/// Kontot kommer ur token, aldrig ur kroppen — en förälder sätter bara sitt eget val. Ingen slug:
/// valet är globalt, inte per lag. Kräver inloggning (§KM.3). Av = ingen push; kritiska besked
/// når ändå fram via mejl.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/notification-settings")]
[Produces("application/json")]
[Authorize]
public sealed class NotificationSettingsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Min globala notisinställning. På om jag aldrig ändrat något.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var settings = await queries
            .SendAsync(new GetNotificationSettingsQuery(actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return settings is null ? Unauthenticated() : Ok(settings);
    }

    /// <summary>Slår på eller av mina notiser. Gäller vid nästa utskick.</summary>
    [HttpPut]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Set(
        NotificationSettingsRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var settings = await commands
            .SendAsync(
                new SetNotificationSettingsCommand(actor.Value, request.Enabled),
                cancellationToken)
            .ConfigureAwait(false);

        return settings is null ? Unauthenticated() : Ok(settings);
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

/// <summary>Den globala på/av-växeln den inloggade skickar in.</summary>
public sealed record NotificationSettingsRequest(bool Enabled);
