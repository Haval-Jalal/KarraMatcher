using KarraMatcher.Api.Features.Administration;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;
using KarraMatcher.Application.Features.Consent;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Consent;

/// <summary>
/// Vårdnadshavarsamtycke (§KM.6, `#195`). Kräver inloggning — samtycket hör till ett konto.
/// </summary>
[ApiController]
[Route("api/v1/consent")]
[Produces("application/json")]
[Authorize]
public sealed class ConsentController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : AdminControllerBase
{
    /// <summary>Den aktuella samtyckestexten, att visa innan man godkänner.</summary>
    [HttpGet("current")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsentTextDto>> Current(CancellationToken cancellationToken)
    {
        var text = await queries.SendAsync(new GetCurrentConsentQuery(), cancellationToken)
            .ConfigureAwait(false);

        return Ok(text);
    }

    /// <summary>Vad den inloggade har samtyckt till, och när.</summary>
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MyConsentDto>> Me(CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var mine = await queries.SendAsync(new GetMyConsentQuery(actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(mine);
    }

    /// <summary>Ger samtycke till den aktuella versionen.</summary>
    [HttpPost]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Grant(ConsentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new GrantConsentCommand(actor.Value, request.Version), cancellationToken)
            .ConfigureAwait(false);

        return outcome == AdminOutcome.Success
            ? NoContent()
            : Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Texten har uppdaterats",
                detail: "Läs den senaste samtyckestexten och godkänn den i stället.");
    }
}

/// <summary>Versionen av samtyckestexten som godkänns.</summary>
public sealed record ConsentRequest(string Version);
