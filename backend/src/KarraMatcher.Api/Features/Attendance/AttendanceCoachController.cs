using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Attendance;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Tränaren kallar till en match (`#57`, §KM.7).
///
/// <h3>Laget står i adressen</h3>
///
/// <para>
/// Under <c>teams/{slug}/matches/{matchId}</c>, precis som tränarens matchhantering. Policyn
/// <c>CoachOfTeam</c> prövas mot slugen, och <see cref="RequireAttendanceEnabledAttribute"/>
/// stänger routen med <c>404</c> när kallelsen är avslagen för laget. Att matchen faktiskt
/// hör till laget prövas dessutom i tjänsten — slugen i adressen räcker inte.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}/matches/{matchId:guid}/attendance")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
[RequireCsrfToken]
[RequireAttendanceEnabled]
public sealed class AttendanceCoachController(ICommandDispatcher commands) : ControllerBase
{
    /// <summary>Öppnar kallelsen för matchen. Idempotent.</summary>
    [HttpPost("call")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OpenCall(
        string slug,
        Guid matchId,
        CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new OpenAttendanceCallCommand(slug, matchId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            OpenCallOutcome.Opened or OpenCallOutcome.AlreadyOpen => NoContent(),

            // Samma svar som for en match som inte finns -- annars gar det att kartlagga
            // vilka match-id som finns i ett annat lag genom att prova.
            _ => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Matchen finns inte",
                detail: "Kontrollera länken — matchen kan ha tagits bort."),
        };
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
