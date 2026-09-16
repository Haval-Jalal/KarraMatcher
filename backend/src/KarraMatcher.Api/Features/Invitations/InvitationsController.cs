using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Invitations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Invitations;

/// <summary>
/// Förälderns sida av en inbjudan (`#193`, §KM.3).
///
/// <para>
/// Förhandsvisningen är anonym — en inbjuden förälder ska kunna se vart länken leder innan
/// hen loggar in (den enda anonyma ytan i den stängda appen utöver inloggningen själv).
/// Själva accepten kräver inloggning, och att den inloggade är just den adress inbjudan
/// gäller.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/invitations")]
[Produces("application/json")]
public sealed class InvitationsController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    /// <summary>Vad inbjudan leder till, för landningssidan. Anonym.</summary>
    [HttpGet("{token}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InvitationPreviewDto>> Preview(
        string token, CancellationToken cancellationToken)
    {
        var preview = await queries
            .SendAsync(new PreviewInvitationQuery(token), cancellationToken)
            .ConfigureAwait(false);

        return preview is null
            ? Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inbjudan finns inte",
                detail: "Länken är fel eller gäller inte längre.")
            : Ok(preview);
    }

    /// <summary>Accepterar inbjudan. Kräver inloggning som rätt adress.</summary>
    [HttpPost("{token}/accept")]
    [Authorize]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<IActionResult> Accept(string token, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sessionen gäller inte längre",
                detail: "Logga in igen.");
        }

        var result = await commands
            .SendAsync(new AcceptInvitationCommand(token, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return result.Outcome switch
        {
            InvitationAcceptOutcome.Accepted => Ok(new AcceptedDto(result.TruppName ?? string.Empty)),
            InvitationAcceptOutcome.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Inbjudan finns inte",
                detail: "Länken är fel eller gäller inte längre."),
            InvitationAcceptOutcome.Expired => Problem(
                statusCode: StatusCodes.Status410Gone,
                title: "Inbjudan har gått ut",
                detail: "Be admin skicka en ny."),
            InvitationAcceptOutcome.AlreadyUsed => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Inbjudan är redan använd",
                detail: "Den här länken har redan accepterats."),
            InvitationAcceptOutcome.EmailMismatch => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Inbjudan gäller en annan adress",
                detail: "Logga in med adressen inbjudan skickades till."),
            _ => Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Okänt fel"),
        };
    }

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>Kvittensen när en inbjudan accepterats — truppens namn, för en vänlig text.</summary>
public sealed record AcceptedDto(string TruppName);
