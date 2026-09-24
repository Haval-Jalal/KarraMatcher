using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Chat;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Chat;

/// <summary>
/// Adminens moderering av chatten i en trupp (§KM.10, `#201`/`#202`). Anmälda meddelanden ur
/// <em>alla</em> kanaler (trupp och lag) syns här, och admin kan radera vilket som helst av
/// dem — kanalobundet, till skillnad från medlemmens egen radering.
/// </summary>
[ApiController]
[Route("api/v1/admin/trupper/{truppId:guid}/chat")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AdminOfTrupp)]
public sealed class ChatAdminController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    /// <summary>Anmälda meddelanden i hela truppen med antal anmälningar, flest först.</summary>
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

    /// <summary>
    /// Tar bort ett anmält meddelande, oavsett kanal (trupp eller lag). Tillåts först när
    /// meddelandet nått tröskeln av anmälningar (`#263`) — annars <c>409</c>.
    /// </summary>
    [HttpDelete("messages/{id:guid}")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var raw = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(raw, out var actorId))
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Sessionen gäller inte längre",
                detail: "Logga in igen.");
        }

        var outcome = await commands
            .SendAsync(new AdminDeleteChatMessageCommand(truppId, id, actorId), cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            ChatModerationOutcome.Ok => NoContent(),
            ChatModerationOutcome.BelowThreshold => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "För få anmälningar",
                detail:
                    $"Ett meddelande kan tas bort först när minst {ChatService.RemovalReportThreshold} "
                    + "anmälningar kommit in."),
            _ => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Meddelandet finns inte",
                detail: "Kontrollera länken — meddelandet kan ha tagits bort."),
        };
    }
}
