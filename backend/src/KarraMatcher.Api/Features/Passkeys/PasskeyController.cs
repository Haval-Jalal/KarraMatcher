using System.Security.Claims;
using System.Text.Json;

using KarraMatcher.Api.Diagnostics;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Passkeys;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Passkeys;

/// <summary>
/// Passkeys (WebAuthn) — inloggning utan lösenord eller kod.
///
/// <para>
/// Registrering kräver inloggning (man lägger till en genväg till sitt <em>eget</em> konto).
/// Inloggning är anonym — det är själva inloggningen — men skyddad av CSRF och rate-limitern som
/// e-postkoden. E-postkoden är kvar som bootstrap och reserv; passkey är en genväg ovanpå.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/passkeys")]
[Produces("application/json")]
public sealed class PasskeyController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    /// <summary>Startar registrering av en passkey för det inloggade kontot.</summary>
    [HttpPost("registrera/start")]
    [Authorize]
    [RequireCsrfToken]
    public async Task<IActionResult> BeginRegistration(CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var optionsJson = await queries
            .SendAsync(new BeginPasskeyRegistrationQuery(actor.Value), cancellationToken)
            .ConfigureAwait(false);

        // Options är redan JSON i WebAuthn:s form — skickas oförändrad till navigator.credentials.
        return Content(optionsJson, "application/json");
    }

    /// <summary>Slutför registreringen och sparar passkey:n.</summary>
    [HttpPost("registrera/klar")]
    [Authorize]
    [RequireCsrfToken]
    public async Task<IActionResult> CompleteRegistration(
        RegisterPasskeyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(
                new CompletePasskeyRegistrationCommand(
                    actor.Value, request.Attestation.GetRawText(), request.DeviceLabel),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome == PasskeyRegistrationOutcome.Registered
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Kunde inte registrera passkey",
                detail: "Försök igen — kontrollera att du är på rätt adress.");
    }

    /// <summary>Startar en passkey-inloggning. Anonym; utmaningen är kortlivad.</summary>
    [HttpPost("logga-in/start")]
    [AllowAnonymous]
    [RequireCsrfToken]
    [EnableRateLimiting(RateLimiting.LoginPolicy)]
    public async Task<ActionResult<PasskeyLoginChallenge>> BeginLogin(CancellationToken cancellationToken)
    {
        var challenge = await queries
            .SendAsync(new BeginPasskeyLoginQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(challenge);
    }

    /// <summary>Slutför inloggningen och utfärdar en session, eller nekar.</summary>
    [HttpPost("logga-in/klar")]
    [AllowAnonymous]
    [RequireCsrfToken]
    [EnableRateLimiting(RateLimiting.LoginPolicy)]
    public async Task<IActionResult> CompleteLogin(
        LoginPasskeyRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await commands
            .SendAsync(
                new CompletePasskeyLoginCommand(request.ChallengeId, request.Assertion.GetRawText()),
                cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            // Ett enda svar oavsett orsak — som e-postkodens, av samma skäl (`#32`).
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Inloggningen gick inte",
                detail: "Försök igen, eller logga in med en kod via mejl.");
        }

        SessionCookie.Write(Response, session.RefreshToken, session.RefreshExpiresUtc);

        return Ok(new SessionResponse(session.AccessToken, session.AccessExpiresUtc));
    }

    /// <summary>Kontots passkeys.</summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<PasskeyDto>>> List(CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var passkeys = await queries
            .SendAsync(new ListPasskeysQuery(actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(passkeys);
    }

    /// <summary>Tar bort en av kontots egna passkeys.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [RequireCsrfToken]
    public async Task<IActionResult> Remove(Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var removed = await commands
            .SendAsync(new RemovePasskeyCommand(actor.Value, id), cancellationToken)
            .ConfigureAwait(false);

        return removed
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Passkey finns inte",
                detail: "Den kan redan ha tagits bort.");
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

/// <summary>Registrerings-svaret från webbläsaren, plus ett valfritt enhetsnamn.</summary>
public sealed record RegisterPasskeyRequest(JsonElement Attestation, string? DeviceLabel);

/// <summary>Inloggnings-svaret från webbläsaren, med utmaningens id.</summary>
public sealed record LoginPasskeyRequest(string ChallengeId, JsonElement Assertion);
