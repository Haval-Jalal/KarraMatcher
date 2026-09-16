using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>Superadmins hantering av klubbar (§KM.3, `#192`). Bara superadmin når hit.</summary>
[ApiController]
[Route("api/v1/admin/clubs")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
[RequireCsrfToken]
public sealed class ClubsAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Alla klubbar.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ClubDto>>> List(CancellationToken cancellationToken)
    {
        var clubs = await queries.SendAsync(new GetClubsQuery(), cancellationToken).ConfigureAwait(false);

        return Ok(clubs);
    }

    /// <summary>Skapar en klubb.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(ClubRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(new CreateClubCommand(request.Name, request.Slug, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, dto => Created($"/api/v1/admin/clubs/{dto.Id}", dto));
    }

    /// <summary>Ändrar en klubbs namn.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id, ClubUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var result = await commands
            .SendAsync(new UpdateClubCommand(id, request.Name, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(result, Ok);
    }
}

/// <summary>Det superadmin fyller i för en ny klubb.</summary>
public sealed record ClubRequest(string Name, string Slug);

/// <summary>Ändring av en klubb — slugen är stabil och ändras inte.</summary>
public sealed record ClubUpdateRequest(string Name);
