using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Superadmins hantering av sporter (§KM.3, `#192`). Bara superadmin når hit.
/// </summary>
[ApiController]
[Route("api/v1/admin/sports")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
[RequireCsrfToken]
public sealed class SportsAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Alla sporter.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SportDto>>> List(CancellationToken cancellationToken)
    {
        var sports = await queries.SendAsync(new GetSportsQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(sports);
    }

    /// <summary>Skapar en sport.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(SportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(new CreateSportCommand(request.Name, request.Slug, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, dto => Created($"/api/v1/admin/sports/{dto.Id}", dto));
    }

    /// <summary>Ändrar en sports namn.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, SportUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(new UpdateSportCommand(id, request.Name, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, Ok);
    }
}

/// <summary>Det superadmin fyller i för en ny sport.</summary>
public sealed record SportRequest(string Name, string Slug);

/// <summary>Ändring av en sport — slugen är stabil och ändras inte.</summary>
public sealed record SportUpdateRequest(string Name);
