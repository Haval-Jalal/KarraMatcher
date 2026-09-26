using System.Security.Claims;

using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Chat;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace KarraMatcher.Api.Features.Chat;

/// <summary>
/// Lag-chatten (`#202`) — en egen kanal per färg-lag ovanpå trupp-chatten (`#201`).
///
/// <para>
/// <c>MemberOfTeam</c> vaktar routen (lagets slug): bara lagets medlemmar läser och skriver.
/// Lagets slug löses upp till kanalen (lag-id + trupp-id) och samma kommandon/tjänst som
/// trupp-chatten används — bara med lag-id satt. Moderering och gallring delas med
/// trupp-chatten: anmälningar syns i truppens admin-kö, och samma gallringsjobb rensar.
/// </para>
/// </summary>
[ApiController]
[Route("api/v1/teams/{slug}/chat")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.MemberOfTeam)]
public sealed class ChatTeamController(
    IQueryDispatcher queries,
    ICommandDispatcher commands) : ControllerBase
{
    /// <summary>Kanal-meta för FE:t: truppens id och om jag är ledare (får schemalägga).</summary>
    [HttpGet("meta")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamChatMetaDto>> Meta(string slug, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var meta = await queries
            .SendAsync(new GetTeamChatMetaQuery(slug, actor.Value), cancellationToken)
            .ConfigureAwait(false);

        return meta is null ? NotFoundForTeam() : Ok(meta);
    }

    /// <summary>De senaste meddelandena i lag-kanalen, äldst först.</summary>
    [HttpGet("messages")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Messages(string slug, CancellationToken cancellationToken)
    {
        var channel = await ResolveAsync(slug, cancellationToken).ConfigureAwait(false);

        if (channel is null)
        {
            return NotFoundForTeam();
        }

        var messages = await queries
            .SendAsync(
                new GetChatMessagesQuery(channel.AgeGroupId, channel.TeamId, ActorId() ?? Guid.Empty),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(messages);
    }

    /// <summary>Mina egna schemalagda meddelanden i lag-kanalen.</summary>
    [HttpGet("scheduled")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Scheduled(string slug, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var channel = await ResolveAsync(slug, cancellationToken).ConfigureAwait(false);

        if (channel is null)
        {
            return NotFoundForTeam();
        }

        var messages = await queries
            .SendAsync(
                new GetScheduledChatMessagesQuery(channel.AgeGroupId, channel.TeamId, actor.Value),
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(messages);
    }

    /// <summary>Postar (eller schemalägger, som ledare) ett meddelande i lag-kanalen.</summary>
    [HttpPost("messages")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Post(
        string slug, PostMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var channel = await ResolveAsync(slug, cancellationToken).ConfigureAwait(false);

        if (channel is null)
        {
            return NotFoundForTeam();
        }

        var outcome = await commands
            .SendAsync(
                new PostChatMessageCommand(
                    channel.AgeGroupId, channel.TeamId, actor.Value, request.Body, request.PublishAt),
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
    public async Task<IActionResult> Delete(string slug, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var channel = await ResolveAsync(slug, cancellationToken).ConfigureAwait(false);

        if (channel is null)
        {
            return NotFoundForTeam();
        }

        var outcome = await commands
            .SendAsync(
                new DeleteChatMessageCommand(channel.AgeGroupId, channel.TeamId, id, actor.Value),
                cancellationToken)
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
        string slug, Guid id, CancellationToken cancellationToken)
    {
        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var channel = await ResolveAsync(slug, cancellationToken).ConfigureAwait(false);

        if (channel is null)
        {
            return NotFoundForTeam();
        }

        var outcome = await commands
            .SendAsync(
                new CancelScheduledChatMessageCommand(
                    channel.AgeGroupId, channel.TeamId, id, actor.Value, IsAdminOf(channel.AgeGroupId)),
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
        string slug, Guid id, ReportMessageRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var channel = await ResolveAsync(slug, cancellationToken).ConfigureAwait(false);

        if (channel is null)
        {
            return NotFoundForTeam();
        }

        var outcome = await commands
            .SendAsync(
                new ReportChatMessageCommand(
                    channel.AgeGroupId, channel.TeamId, id, actor.Value, request.Reason),
                cancellationToken)
            .ConfigureAwait(false);

        return Respond(outcome);
    }

    /// <summary>Växlar min reaktion (emoji) på ett meddelande i lag-kanalen av och på (`#301`).</summary>
    [HttpPost("messages/{id:guid}/reactions")]
    [RequireCsrfToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> React(
        string slug, Guid id, ReactRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var actor = ActorId();

        if (actor is null)
        {
            return Unauthenticated();
        }

        var channel = await ResolveAsync(slug, cancellationToken).ConfigureAwait(false);

        if (channel is null)
        {
            return NotFoundForTeam();
        }

        var outcome = await commands
            .SendAsync(
                new ToggleReactionCommand(
                    channel.AgeGroupId, channel.TeamId, id, actor.Value, request.Emoji ?? string.Empty),
                cancellationToken)
            .ConfigureAwait(false);

        return outcome switch
        {
            ChatModerationOutcome.Ok => NoContent(),
            ChatModerationOutcome.NotAllowed => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Reaktionen stöds inte",
                detail: "Välj en av de tillgängliga reaktionerna."),
            _ => NotFoundForTeam(),
        };
    }

    private Task<TeamChannel?> ResolveAsync(string slug, CancellationToken cancellationToken) =>
        queries.SendAsync(new GetTeamChannelQuery(slug), cancellationToken);

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

    private ObjectResult NotFoundForTeam() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Laget finns inte",
        detail: "Kontrollera länken — laget kan ha bytt namn.");

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
