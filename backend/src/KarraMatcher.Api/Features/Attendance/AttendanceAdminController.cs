using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Attendance;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Spaken som släpper på kallelsen för ett lag (`#56`, §KM.7).
///
/// <h3>Administratör, inte tränare</h3>
///
/// <para>
/// Att slå på kallelsen är att börja behandla uppgifter om barn på servern — tränaren
/// lägger upp en trupp med förnamn. Det beslutet hör till klubben och inte till en enskild
/// tränare, och det förutsätter dessutom att samtyckesrutinen är på plats (§KM.6, `#59`).
/// </para>
///
/// <h3>Adressen ligger under <c>admin</c> med flit</h3>
///
/// <para>
/// Den skiljer sig därmed från tränarens <c>api/v1/teams/{slug}/...</c>, och den skillnaden
/// syns i routen i stället för att bara stå i ett attribut. Det gör den svår att blanda
/// ihop med något en tränare får göra.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/teams/{slug}/attendance")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[RequireCsrfToken]
public sealed class AttendanceAdminController(ICommandDispatcher commands) : ControllerBase
{
    /// <summary>Slår på eller av kallelsen för laget.</summary>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetEnabled(
        string slug,
        AttendanceFlagRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sessionen gäller inte längre",
                detail: "Logga in igen.");
        }

        var changed = await commands
            .SendAsync(
                new SetAttendanceEnabledCommand(slug, request.Enabled, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return changed
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Laget finns inte",
                detail: "Kontrollera adressen.");
    }

    private Guid? ActorId()
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>Av eller på. Inget mer — en flagga har inga inställningar.</summary>
public sealed record AttendanceFlagRequest(bool Enabled);
