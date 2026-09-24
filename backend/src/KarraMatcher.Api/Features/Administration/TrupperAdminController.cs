using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Superadmins hantering av trupper (kodnamn <c>AgeGroup</c>) (§KM.3, `#192`). Bara
/// superadmin når hit.
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
[RequireCsrfToken]
public sealed class TrupperAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Trupperna, valfritt filtrerade på klubb.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TruppDto>>> List(
        [FromQuery] Guid? clubId, CancellationToken cancellationToken)
    {
        var trupper = await queries.SendAsync(new GetTrupperQuery(clubId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(trupper);
    }

    /// <summary>Skapar en trupp under en klubb och en sport.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(TruppRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new CreateTruppCommand(
                    request.ClubId, request.SportId, request.Name, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, dto => Created($"/api/v1/admin/trupper/{dto.Id}", dto));
    }

    /// <summary>Ändrar en trupps sport, namn och säsong.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id, TruppUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new UpdateTruppCommand(id, request.SportId, request.Name, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, Ok);
    }
}

/// <summary>Det superadmin fyller i för en ny trupp.</summary>
public sealed record TruppRequest(Guid ClubId, Guid SportId, string Name);

/// <summary>Ändring av en trupp — klubben byter den inte.</summary>
public sealed record TruppUpdateRequest(Guid SportId, string Name);
