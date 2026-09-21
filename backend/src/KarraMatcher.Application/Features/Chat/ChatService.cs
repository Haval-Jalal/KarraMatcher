using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Chat;

namespace KarraMatcher.Application.Features.Chat;

/// <summary>Vad ett försök att posta ett meddelande slutade med.</summary>
public enum ChatPostOutcome
{
    /// <summary>Postat och synligt nu.</summary>
    Posted = 0,

    /// <summary>Schemalagt för en framtida tid.</summary>
    Scheduled = 1,

    /// <summary>Bara admin/tränare får schemalägga.</summary>
    NotLeaderForSchedule = 2,
}

/// <summary>Utfall för radering/anmälan/avbokning.</summary>
public enum ChatModerationOutcome
{
    Ok = 0,
    NotFound = 1,
    NotAllowed = 2,
}

/// <summary>
/// Trupp-chatten (§KM.1/§KM.10, `#201`).
///
/// <para>
/// Medlemmar skriver och läser (grinden <c>MemberOfTrupp</c> i API:t). Admin/tränare kan
/// dessutom schemalägga ett meddelande till en framtida tid. Radering töms texten direkt och
/// lämnar en "[borttaget]"-markering; anmälningar syns för truppens admin. Meddelandetexten
/// loggas aldrig (§KM.10) — audit bär bara meddelandets id och åtgärden.
/// </para>
/// </summary>
public sealed class ChatService(
    IChatRepository chat,
    IMembershipService membership,
    IAccountRepository accounts,
    IAuditLog audit,
    IPushOutbox push,
    TimeProvider clock)
{
    /// <summary>
    /// Postar nu, eller schemalägger om en framtida tid anges (kräver ledare). Kanalen är
    /// truppen (<paramref name="teamId"/> null) eller ett lag i truppen (`#202`).
    /// </summary>
    public async Task<ChatPostOutcome> PostAsync(
        Guid truppId,
        Guid? teamId,
        Guid accountId,
        string body,
        DateTimeOffset? publishAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);

        var now = clock.GetUtcNow().UtcDateTime;
        var scheduled = publishAt is { } at && at.UtcDateTime > now;

        if (scheduled
            && !await membership.IsLeaderOfTruppAsync(accountId, truppId, cancellationToken)
                .ConfigureAwait(false))
        {
            return ChatPostOutcome.NotLeaderForSchedule;
        }

        var publishAtUtc = scheduled ? publishAt!.Value.UtcDateTime : now;

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            AgeGroupId = truppId,
            TeamId = teamId,
            AuthorAccountId = accountId,
            Body = body.Trim(),
            CreatedUtc = now,
            PublishAtUtc = publishAtUtc,
            PublishedUtc = scheduled ? null : now,
        };

        await chat.AddMessageAsync(message, cancellationToken).ConfigureAwait(false);
        await chat.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (!scheduled)
        {
            await NotifyAsync(truppId, teamId, accountId, cancellationToken).ConfigureAwait(false);
        }

        return scheduled ? ChatPostOutcome.Scheduled : ChatPostOutcome.Posted;
    }

    /// <summary>De senaste publicerade meddelandena i kanalen (äldst först).</summary>
    public async Task<IReadOnlyList<ChatMessageDto>> ListAsync(
        Guid truppId, Guid? teamId, CancellationToken cancellationToken)
    {
        var messages = await chat.ListPublishedAsync(truppId, teamId, 100, cancellationToken)
            .ConfigureAwait(false);

        var names = await accounts
            .DisplayNamesAsync([.. messages.Select(m => m.AuthorAccountId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return [.. messages.Select(m => ToDto(m, names))];
    }

    /// <summary>Den inloggades egna schemalagda meddelanden i kanalen.</summary>
    public async Task<IReadOnlyList<ScheduledMessageDto>> ListScheduledAsync(
        Guid truppId, Guid? teamId, Guid accountId, CancellationToken cancellationToken)
    {
        var messages = await chat
            .ListScheduledForAuthorAsync(truppId, teamId, accountId, cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. messages.Select(m => new ScheduledMessageDto(
                m.Id, m.Body, new DateTimeOffset(m.PublishAtUtc, TimeSpan.Zero))),
        ];
    }

    /// <summary>
    /// Adminens moderering: anmälda meddelanden i hela truppen — trupp-kanalen och alla dess
    /// lag-kanaler (`#202`: moderering delas med trupp-chatten).
    /// </summary>
    public async Task<IReadOnlyList<ReportedMessageDto>> ListReportedAsync(
        Guid truppId, CancellationToken cancellationToken)
    {
        var rows = await chat.ListReportedForTruppAsync(truppId, cancellationToken).ConfigureAwait(false);

        var names = await accounts
            .DisplayNamesAsync([.. rows.Select(r => r.AuthorAccountId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(r => new ReportedMessageDto(
                r.MessageId,
                r.AuthorAccountId,
                names.TryGetValue(r.AuthorAccountId, out var name) ? name : null,
                r.Deleted ? string.Empty : r.Body,
                r.Deleted,
                r.ReportCount)),
        ];
    }

    /// <summary>Tar bort ett meddelande: eget alltid, annat om anroparen är admin. Töms direkt.</summary>
    public async Task<ChatModerationOutcome> DeleteAsync(
        Guid truppId,
        Guid? teamId,
        Guid messageId,
        Guid accountId,
        bool actorIsAdmin,
        CancellationToken cancellationToken)
    {
        var message = await chat.FindMessageAsync(messageId, cancellationToken).ConfigureAwait(false);

        if (message is null || message.AgeGroupId != truppId || message.TeamId != teamId)
        {
            return ChatModerationOutcome.NotFound;
        }

        if (message.AuthorAccountId != accountId && !actorIsAdmin)
        {
            return ChatModerationOutcome.NotAllowed;
        }

        if (message.DeletedUtc is null)
        {
            message.DeletedUtc = clock.GetUtcNow().UtcDateTime;
            message.DeletedByAccountId = accountId;
            message.Body = string.Empty; // Texten (PII) tas bort direkt; tombstonen blir kvar.

            await audit.RecordAsync(
                AuditActions.ChatMessageDeleted, accountId, cancellationToken, messageId)
                .ConfigureAwait(false);

            await chat.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ChatModerationOutcome.Ok;
    }

    /// <summary>
    /// Adminens moderering: tar bort valfritt meddelande i truppen, oavsett kanal (`#202`).
    ///
    /// <para>
    /// Anmälningskön spänner hela truppen (trupp-kanalen och alla lag-kanaler), så adminens
    /// radering får inte vara kanalbunden. Behörigheten är redan prövad (AdminOfTrupp); här
    /// räcker att meddelandet hör till truppen. Texten töms direkt, tombstonen blir kvar.
    /// </para>
    /// </summary>
    public async Task<ChatModerationOutcome> DeleteByAdminAsync(
        Guid truppId,
        Guid messageId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var message = await chat.FindMessageAsync(messageId, cancellationToken).ConfigureAwait(false);

        if (message is null || message.AgeGroupId != truppId)
        {
            return ChatModerationOutcome.NotFound;
        }

        if (message.DeletedUtc is null)
        {
            message.DeletedUtc = clock.GetUtcNow().UtcDateTime;
            message.DeletedByAccountId = actorAccountId;
            message.Body = string.Empty;

            await audit.RecordAsync(
                AuditActions.ChatMessageDeleted, actorAccountId, cancellationToken, messageId)
                .ConfigureAwait(false);

            await chat.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ChatModerationOutcome.Ok;
    }

    /// <summary>Avbokar ett eget (eller, som admin, valfritt) schemalagt meddelande innan det går ut.</summary>
    public async Task<ChatModerationOutcome> CancelScheduledAsync(
        Guid truppId,
        Guid? teamId,
        Guid messageId,
        Guid accountId,
        bool actorIsAdmin,
        CancellationToken cancellationToken)
    {
        var message = await chat.FindMessageAsync(messageId, cancellationToken).ConfigureAwait(false);

        if (message is null
            || message.AgeGroupId != truppId
            || message.TeamId != teamId
            || message.PublishedUtc is not null
            || message.DeletedUtc is not null)
        {
            return ChatModerationOutcome.NotFound;
        }

        if (message.AuthorAccountId != accountId && !actorIsAdmin)
        {
            return ChatModerationOutcome.NotAllowed;
        }

        // Aldrig publicerat — raden kan tas bort helt.
        chat.RemoveMessage(message);
        await chat.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ChatModerationOutcome.Ok;
    }

    /// <summary>Anmäler ett publicerat meddelande. Idempotent per anmälare.</summary>
    public async Task<ChatModerationOutcome> ReportAsync(
        Guid truppId,
        Guid? teamId,
        Guid messageId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var message = await chat.FindMessageAsync(messageId, cancellationToken).ConfigureAwait(false);

        if (message is null
            || message.AgeGroupId != truppId
            || message.TeamId != teamId
            || message.PublishedUtc is null)
        {
            return ChatModerationOutcome.NotFound;
        }

        if (!await chat.ReportExistsAsync(messageId, accountId, cancellationToken).ConfigureAwait(false))
        {
            await chat.AddReportAsync(
                new ChatReport
                {
                    Id = Guid.NewGuid(),
                    MessageId = messageId,
                    ReportedByAccountId = accountId,
                    CreatedUtc = clock.GetUtcNow().UtcDateTime,
                },
                cancellationToken).ConfigureAwait(false);

            await audit.RecordAsync(
                AuditActions.ChatMessageReported, accountId, cancellationToken, messageId)
                .ConfigureAwait(false);

            await chat.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return ChatModerationOutcome.Ok;
    }

    /// <summary>Släpper schemalagda meddelanden vars tid passerat och notifierar. Ger antalet.</summary>
    public async Task<int> ReleaseDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var due = await chat.ListDueForReleaseAsync(now, cancellationToken).ConfigureAwait(false);

        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var message in due)
        {
            message.PublishedUtc = now;
        }

        await chat.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (var message in due)
        {
            await NotifyAsync(
                message.AgeGroupId, message.TeamId, message.AuthorAccountId, cancellationToken)
                .ConfigureAwait(false);
        }

        return due.Count;
    }

    /// <summary>
    /// Notiserar kanalens medlemmar (utom författaren) om ett nytt meddelande — hela truppen
    /// för trupp-kanalen, lagets medlemmar för en lag-kanal (`#202`).
    ///
    /// <para>
    /// Chatt-inställningen är per lag men tolkas konservativt: har man stängt av Chatt för
    /// något lag i truppen får man ingen chatt-notis. Mottagarna är förfiltrerade här; ett
    /// lag-id skickas med bara för att leverans-lagret ska ha en nyckel — filtret där blir då
    /// tomt. Aldrig ett barns namn eller meddelandetexten i notisen (§KM.1/§KM.10).
    /// </para>
    /// </summary>
    private async Task NotifyAsync(
        Guid truppId, Guid? teamId, Guid authorAccountId, CancellationToken cancellationToken)
    {
        var members = teamId is null
            ? await membership.MemberAccountIdsForTruppAsync(truppId, cancellationToken)
                .ConfigureAwait(false)
            : await membership.MemberAccountIdsAsync(teamId.Value, cancellationToken)
                .ConfigureAwait(false);

        var disabled = (await chat.ChatDisabledAccountIdsAsync(truppId, cancellationToken)
            .ConfigureAwait(false)).ToHashSet();

        var recipients = members
            .Where(id => id != authorAccountId && !disabled.Contains(id))
            .ToArray();

        if (recipients.Length == 0)
        {
            return;
        }

        // Ett lag-id att fästa notisen vid (leverans-lagrets per-lag-nyckel). För en lag-kanal
        // är det laget självt; för trupp-kanalen räcker vilket lag som helst i truppen.
        var pushTeamId = teamId ?? await chat.AnyTeamIdAsync(truppId, cancellationToken)
            .ConfigureAwait(false);

        if (pushTeamId is null)
        {
            return;
        }

        push.Enqueue(PushDispatch.ToAccounts(
            pushTeamId.Value,
            recipients,
            PushCategory.Chat,
            new PushMessage(
                "Nytt meddelande i chatten",
                "Öppna för att läsa.",
                $"/chatt/{truppId}")));
    }

    private static ChatMessageDto ToDto(ChatMessage message, IReadOnlyDictionary<Guid, string> names) =>
        new(
            message.Id,
            message.AuthorAccountId,
            names.TryGetValue(message.AuthorAccountId, out var name) ? name : null,
            message.DeletedUtc is null ? message.Body : string.Empty,
            new DateTimeOffset(message.PublishedUtc ?? message.PublishAtUtc, TimeSpan.Zero),
            message.DeletedUtc is not null);
}
