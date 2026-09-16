using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Superadmins hantering av lag (kodnamn <c>Team</c>) (§KM.3, `#192`). Bara superadmin når hit.
/// </summary>
[ApiController]
[Route("api/v1/admin/lag")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
[RequireCsrfToken]
public sealed class LagAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Lagen i en trupp.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LagDto>>> List(
        [FromQuery] Guid truppId, CancellationToken cancellationToken)
    {
        var lag = await queries.SendAsync(new GetLagQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(lag);
    }

    /// <summary>Skapar ett lag under en trupp.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(LagRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new CreateLagCommand(
                    request.TruppId, request.Name, request.ColorHex, request.Slug, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, dto => Created($"/api/v1/admin/lag/{dto.Id}", dto));
    }

    /// <summary>Ändrar ett lags namn och färg.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, LagUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(
                new UpdateLagCommand(id, request.Name, request.ColorHex, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, Ok);
    }
}

/// <summary>Det superadmin fyller i för ett nytt lag.</summary>
public sealed record LagRequest(Guid TruppId, string Name, string ColorHex, string Slug);

/// <summary>Ändring av ett lag — slugen är stabil och truppen byter den inte.</summary>
public sealed record LagUpdateRequest(string Name, string ColorHex);
