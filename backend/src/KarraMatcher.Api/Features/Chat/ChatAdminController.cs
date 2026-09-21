using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Chat;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Features.Chat;

/// <summary>
/// Adminens moderering av trupp-chatten (§KM.10, `#201`). Anmälda meddelanden syns här; att
/// radera dem sker via medlems-endpointen (en admin är också medlem och når den).
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/chat")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
public sealed class ChatAdminController(IQueryDispatcher queries) : ControllerBase
{
    /// <summary>Anmälda meddelanden med antal anmälningar, flest först.</summary>
    [HttpGet("reports")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReportedMessageDto>>> Reports(
        Guid truppId, CancellationToken cancellationToken)
    {
        var reports = await queries
            .SendAsync(new GetReportedChatMessagesQuery(truppId), cancellationToken)
            .ConfigureAwait(false);

        return Ok(reports);
    }
}
