using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Push;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Push;

/// <summary>
/// En förälders notisinställningar för ett lag (`#65`).
///
/// <h3>Kontot är den inloggade, laget står i adressen</h3>
///
/// <para>
/// Vilket konto det gäller kommer ur token, aldrig ur kroppen — en förälder kan bara sätta
/// sina egna val. Laget kommer ur adressen. Kräver inloggning: en inställning hör till ett
/// konto, och en gäst har inget att spara på (§KM.3).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}/notification-settings")]
[Produces("application/json")]
[Authorize]
public sealed class NotificationSettingsController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Mina notisval för laget. Allt på om jag aldrig ändrat något.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string slug, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var settings = await queries
            .SendAsync(new GetNotificationSettingsQuery(slug, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return settings is null ? TeamNotFound() : Ok(settings);
    }

    /// <summary>Sätter mina notisval för laget. Avstängning gäller vid nästa utskick.</summary>
    [HttpPut]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Set(
        string slug,
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
                new SetNotificationSettingsCommand(slug, actor.Value, request.ToDraft()),
                cancellationToken)
            .ConfigureAwait(false);

        return settings is null ? TeamNotFound() : Ok(settings);
    }

    private ObjectResult TeamNotFound() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Laget finns inte",
        detail: "Kontrollera adressen.");

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

/// <summary>De tre växlarna den inloggade skickar in.</summary>
public sealed record NotificationSettingsRequest(bool MatchChanges, bool Carpool, bool Reminders)
{
    internal NotificationSettingsDraft ToDraft() => new(MatchChanges, Carpool, Reminders);
}
