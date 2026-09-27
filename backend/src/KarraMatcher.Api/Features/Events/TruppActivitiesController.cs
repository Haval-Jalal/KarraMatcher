using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Events.GetTruppActivities;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Events;

/// <summary>
/// Truppens aktivitetslista (`#334`, epic #330): alla händelser i truppen — matcher, träningar,
/// cuper och övrigt, tvärs över färg-lagen och inklusive trupp-vida — i avsparksordning.
///
/// <para>
/// En trupp-vid vy, så den vaktas av <c>MemberOfTrupp</c>, inte av en lag-policy: en förälder
/// vars barn ligger i ett färg-lag ska ändå se hela truppens aktiviteter (§KM.3). Läsning för
/// alla medlemmar; skapande sker via admin-routen (<c>TruppEventAdminController</c>). Rollen styr
/// bara vad klienten <em>visar</em> — servern grindar åtkomsten.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/trupper/{truppId:guid}/events")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.MemberOfTrupp)]
public sealed class TruppActivitiesController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Truppens aktiviteter i avsparksordning.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<TruppActivityDto>>> List(
        Guid truppId, CancellationToken cancellationToken)
    {
        var activities = await queries
            .SendAsync(new GetTruppActivitiesQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(activities);
    }
}
