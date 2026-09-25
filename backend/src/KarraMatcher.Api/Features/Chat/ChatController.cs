using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Chat;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Chat;

/// <summary>
/// Trupp-chatten för medlemmar (§KM.1/§KM.10, `#201`).
///
/// <para>
/// <c>MemberOfTrupp</c> vaktar routen: bara truppens medlemmar läser och skriver, en
/// icke-medlem nekas. En admin/tränare kan dessutom schemalägga (server prövar ledarskap).
/// Radering: eget meddelande alltid, annat om anroparen är admin för truppen.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/trupper/{truppId:guid}/chat")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.MemberOfTrupp)]
public sealed class ChatController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    /// <summary>
    /// Kanalerna den inloggade får se i truppen: primärkanalen ("Truppen") först, sedan de
    /// lag-kanaler kontot når (`#293`). Låter klienten rita en kanalväxlare utan att själv
    /// härleda behörighet — servern avgör vad som kommer med.
    /// </summary>
    [HttpGet("channels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ChatChannelDto>>> Channels(
        Guid truppId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var channels = await queries
            .SendAsync(new GetChatChannelsQuery(truppId, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(channels);
    }

    /// <summary>De senaste meddelandena, äldst först.</summary>
    [HttpGet("messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> Messages(
        Guid truppId, CancellationToken cancellationToken)
    {
        var messages = await queries
            .SendAsync(new GetChatMessagesQuery(truppId, null), cancellationToken)
            .ConfigureAwait(false);

        return Ok(messages);
    }

    /// <summary>Mina egna schemalagda (ännu ej utskickade) meddelanden.</summary>
    [HttpGet("scheduled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Scheduled(Guid truppId, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var messages = await queries
            .SendAsync(new GetScheduledChatMessagesQuery(truppId, null, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Ok(messages);
    }

    /// <summary>Postar ett meddelande nu, eller schemalägger om en framtida tid anges.</summary>
    [HttpPost("messages")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Post(
        Guid truppId, PostMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(
                new PostChatMessageCommand(truppId, null, actor.Value, request.Body, request.PublishAt),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            ChatPostOutcome.Posted or ChatPostOutcome.Scheduled => NoContent(),
            _ => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Bara ledare kan schemalägga",
                detail: "Att skicka vid en framtida tid är för admin och tränare."),
        };
    }

    /// <summary>Tar bort ett eget meddelande (admin raderar andras bara ur anmälningskön, `#263`).</summary>
    [HttpDelete("messages/{id:guid}")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(new DeleteChatMessageCommand(truppId, null, id, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome);
    }

    /// <summary>Avbokar ett schemalagt meddelande innan det går ut.</summary>
    [HttpDelete("scheduled/{id:guid}")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelScheduled(
        Guid truppId, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(
                new CancelScheduledChatMessageCommand(truppId, null, id, actor.Value, IsAdminOf(truppId)),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome);
    }

    /// <summary>Anmäler ett meddelande med en obligatorisk motivering (`#263`). Idempotent.</summary>
    [HttpPost("messages/{id:guid}/report")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(
        Guid truppId, Guid id, ReportMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var outcome = await commands
            .SendAsync(
                new ReportChatMessageCommand(truppId, null, id, actor.Value, request.Reason),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome);
    }

    private IActionResult Respond(ChatModerationOutcome outcome) => outcome switch
    {
        ChatModerationOutcome.Ok => NoContent(),
        ChatModerationOutcome.NotAllowed => Problem(
            statusCode: StatusCodes.Status403Forbidden,
            title: "Inte tillåtet",
            detail: "Du får inte göra det här."),
        _ => Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Meddelandet finns inte",
            detail: "Kontrollera länken — meddelandet kan ha tagits bort."),
    };

    private bool IsAdminOf(Guid truppId) =>
        User.HasClaim(AuthClaims.SuperAdmin, "true")
        || User.HasClaim(AuthClaims.AdminOfTrupp, truppId.ToString());

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

/// <summary>Det medlemmen skickar: texten och en valfri framtida utskickstid (schemaläggning).</summary>
public sealed record PostMessageRequest(string Body, DateTimeOffset? PublishAt);

/// <summary>Det en anmälan bär: en obligatorisk motivering (`#263`). Fritext, prövas server-side.</summary>
public sealed record ReportMessageRequest(string Reason);
