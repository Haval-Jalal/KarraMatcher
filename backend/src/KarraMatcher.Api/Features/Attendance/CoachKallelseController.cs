using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Attendance;
using KarraMatcher.Application.Features.Children;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Färg-lag-tränarens kallelse (§KM.7, `#redesign`).
///
/// <para>
/// En tränare sköter <b>sitt eget färg-lags</b> matcher och träningar. Laget står i adressen
/// och grinden är <c>CoachOfTeam</c> — en tränare för Gul når aldrig Blås händelser, och aldrig
/// en annan trupp/klubb (§KM.3). Servern verifierar dessutom att händelsen hör till tränarens
/// lag (<c>RequireEventTeamId</c>).
/// </para>
///
/// <para>
/// <b>Mottagarna får vara hela truppen</b> — tränaren kan fylla på enskilda individer ur andra
/// färg-lag i sin egen match. Därför finns även en läsvy över truppens barn (utan
/// vårdnadshavare, §KM.1) för väljaren. Admin behåller sin trupp-breda vy (alla händelser).
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.CoachOfTeam)]
[RequireCsrfToken]
public sealed class CoachKallelseController(
    ICommandDispatcher commands,
    IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Truppens barn (utan vårdnadshavare) för kallelse-väljaren.</summary>
    [HttpGet("kallelse-roster")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Roster(string slug, CancellationToken cancellationToken)
    {
        var team = await ResolveTeamAsync(slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return TeamNotFound();
        }

        var roster = await queries
            .SendAsync(new GetKallelseRosterQuery(team.TruppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(roster);
    }

    /// <summary>Skickar/uppdaterar kallelsen för lagets händelse. Mottagare får vara hela truppen.</summary>
    [HttpPut("events/{eventId:guid}/kallelse")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Set(
        string slug,
        Guid eventId,
        SetKallelseRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var team = await ResolveTeamAsync(slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return TeamNotFound();
        }

        var outcome = await commands
            .SendAsync(
                new SetKallelseCommand(
                    team.TruppId, eventId, request.ChildIds ?? [], actor.Value, team.TeamId),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            SetKallelseOutcome.Set => NoContent(),

            SetKallelseOutcome.InvalidChild => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Ogiltigt barn",
                detail: "Ett valt barn hör inte till truppen."),

            SetKallelseOutcome.EventNotInvitable => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Kallelse gäller inte den här händelsen",
                detail: "Bara matcher och träningar kan ha en kallelse — inte övriga händelser (§KM.7)."),

            // EventNotInTrupp: händelsen finns inte, hör till en annan trupp, eller är inte det
            // här lagets — samma 404, avslöja inte andras id.
            _ => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Händelsen finns inte",
                detail: "Kontrollera länken — händelsen kan ha tagits bort eller höra till ett annat lag."),
        };
    }

    /// <summary>Sammanställningen för lagets händelse: kallade barn med svar.</summary>
    [HttpGet("events/{eventId:guid}/kallelse")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KallelseSummaryDto>> Summary(
        string slug,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var team = await ResolveTeamAsync(slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return TeamNotFound();
        }

        var summary = await queries
            .SendAsync(new GetKallelseSummaryQuery(team.TruppId, eventId, team.TeamId), cancellationToken)
            .ConfigureAwait(false);

        return summary is null ? EventNotFound() : Ok(summary);
    }

    /// <summary>Påminner de kallade barn i lagets händelse som inte svarat.</summary>
    [HttpPost("events/{eventId:guid}/kallelse/remind")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remind(
        string slug,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var team = await ResolveTeamAsync(slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return TeamNotFound();
        }

        var reminded = await commands
            .SendAsync(
                new RemindNonRespondersCommand(team.TruppId, eventId, team.TeamId), cancellationToken)
            .ConfigureAwait(false);

        return reminded is null
            ? EventNotFound()
            : Ok(new AttendanceRemindResult(reminded.Value));
    }

    private Task<TeamRefDto?> ResolveTeamAsync(string slug, CancellationToken cancellationToken) =>
        queries.SendAsync(new GetTeamRefBySlugQuery(slug), cancellationToken);

    private ObjectResult TeamNotFound() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Laget finns inte",
        detail: "Kontrollera adressen.");

    private ObjectResult EventNotFound() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Händelsen finns inte",
        detail: "Kontrollera länken — händelsen kan ha tagits bort eller höra till ett annat lag.");

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
