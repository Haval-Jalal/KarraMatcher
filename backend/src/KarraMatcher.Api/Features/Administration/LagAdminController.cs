using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Administration;

/// <summary>
/// Hantering av lag (kodnamn <c>Team</c>) i en trupp (§KM.3, `#192`, `#261`).
///
/// <para>
/// Lagen är truppens admins ansvar, inte superadmins: hen skapar färg-lagen och sköter dem.
/// Därför ligger endpointen under truppen och gården är <c>AdminOfTrupp</c> — superadmin
/// kortsluts av den policyn, så ägaren når fortfarande hit vid behov.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/lag")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
[RequireCsrfToken]
public sealed class LagAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Lagen i truppen.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<LagDto>>> List(
        Guid truppId, CancellationToken cancellationToken)
    {
        var lag = await queries.SendAsync(new GetLagQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(lag);
    }

    /// <summary>Skapar ett lag under truppen.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        Guid truppId, LagRequest request, CancellationToken cancellationToken)
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
                    truppId, request.Name, request.ColorHex, request.Slug, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(
            result, dto => Created($"/api/v1/admin/trupper/{truppId}/lag/{dto.Id}", dto));
    }

    /// <summary>Ändrar ett lags namn och färg.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid truppId, Guid id, LagUpdateRequest request, CancellationToken cancellationToken)
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

/// <summary>Det admin fyller i för ett nytt lag; truppen kommer från adressen.</summary>
public sealed record LagRequest(string Name, string ColorHex, string Slug);

/// <summary>Ändring av ett lag — slugen är stabil och truppen byter den inte.</summary>
public sealed record LagUpdateRequest(string Name, string ColorHex);
