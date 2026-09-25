using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Chat;

/*
 * Kanalen är paret (TruppId, TeamId?): TeamId null = trupp-chatten, satt = en lag-kanal
 * (`#202`). Samma kommandon och samma tjänst betjänar båda; controllern för lag-kanalen
 * löser upp lagets slug till (TeamId, TruppId) via GetTeamChannelQuery först.
 */

/// <summary>Postar (eller schemalägger) ett meddelande i en kanal.</summary>
public sealed record PostChatMessageCommand(
    Guid TruppId,
    Guid? TeamId,
    Guid AccountId,
    string Body,
    DateTimeOffset? PublishAt) : ICommand<ChatPostOutcome>;

/// <summary>Tar bort ett eget meddelande (`#263`: admin raderar andras bara ur kön, vid tröskeln).</summary>
public sealed record DeleteChatMessageCommand(
    Guid TruppId,
    Guid? TeamId,
    Guid MessageId,
    Guid AccountId) : ICommand<ChatModerationOutcome>;

/// <summary>Avbokar ett schemalagt meddelande innan det går ut.</summary>
public sealed record CancelScheduledChatMessageCommand(
    Guid TruppId,
    Guid? TeamId,
    Guid MessageId,
    Guid AccountId,
    bool ActorIsAdmin) : ICommand<ChatModerationOutcome>;

/// <summary>Anmäler ett meddelande med en obligatorisk motivering (`#263`).</summary>
public sealed record ReportChatMessageCommand(
    Guid TruppId,
    Guid? TeamId,
    Guid MessageId,
    Guid AccountId,
    string Reason) : ICommand<ChatModerationOutcome>;

/// <summary>Adminens moderering: tar bort valfritt meddelande i truppen, oavsett kanal (`#202`).</summary>
public sealed record AdminDeleteChatMessageCommand(Guid TruppId, Guid MessageId, Guid ActorAccountId)
    : ICommand<ChatModerationOutcome>;

/// <summary>De senaste meddelandena i en kanal.</summary>
public sealed record GetChatMessagesQuery(Guid TruppId, Guid? TeamId)
    : IQuery<IReadOnlyList<ChatMessageDto>>;

/// <summary>Den inloggades egna schemalagda meddelanden i en kanal.</summary>
public sealed record GetScheduledChatMessagesQuery(Guid TruppId, Guid? TeamId, Guid AccountId)
    : IQuery<IReadOnlyList<ScheduledMessageDto>>;

/// <summary>Adminens kö av anmälda meddelanden i hela truppen.</summary>
public sealed record GetReportedChatMessagesQuery(Guid TruppId)
    : IQuery<IReadOnlyList<ReportedMessageDto>>;

/// <summary>Kanalerna den inloggade får se i truppen: primärkanalen + nåbara lag-kanaler (`#293`).</summary>
public sealed record GetChatChannelsQuery(Guid TruppId, Guid AccountId)
    : IQuery<IReadOnlyList<ChatChannelDto>>;

/// <summary>Löser upp ett lags slug till dess chatt-kanal (TeamId + TruppId). Null när laget saknas.</summary>
public sealed record GetTeamChannelQuery(string Slug) : IQuery<TeamChannel?>;

/// <summary>Meta om en lag-kanal för den inloggade (trupp-id + ledarskap). Null när laget saknas.</summary>
public sealed record GetTeamChatMetaQuery(string Slug, Guid AccountId) : IQuery<TeamChatMetaDto?>;

internal sealed class PostChatMessageCommandValidator : AbstractValidator<PostChatMessageCommand>
{
    /// <summary>Taket på ett meddelande. Prövas server-side, inte bara i formuläret.</summary>
    public const int MaxBody = 2000;

    public PostChatMessageCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
        RuleFor(c => c.Body)
            .NotEmpty().WithMessage("Skriv ett meddelande.")
            .MaximumLength(MaxBody).WithMessage("Meddelandet är för långt.");
    }
}

internal sealed class DeleteChatMessageCommandValidator : AbstractValidator<DeleteChatMessageCommand>
{
    public DeleteChatMessageCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
    }
}

internal sealed class CancelScheduledChatMessageCommandValidator
    : AbstractValidator<CancelScheduledChatMessageCommand>
{
    public CancelScheduledChatMessageCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
    }
}

internal sealed class ReportChatMessageCommandValidator : AbstractValidator<ReportChatMessageCommand>
{
    public ReportChatMessageCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
        RuleFor(c => c.Reason)
            .NotEmpty().WithMessage("Skriv varför du anmäler meddelandet.")
            .MaximumLength(Domain.Chat.ChatReport.MaxReason).WithMessage("Motiveringen är för lång.");
    }
}

internal sealed class GetScheduledChatMessagesQueryValidator
    : AbstractValidator<GetScheduledChatMessagesQuery>
{
    public GetScheduledChatMessagesQueryValidator()
    {
        RuleFor(q => q.TruppId).NotEmpty();
        RuleFor(q => q.AccountId).NotEmpty();
    }
}

internal sealed class GetTeamChannelQueryValidator : AbstractValidator<GetTeamChannelQuery>
{
    public GetTeamChannelQueryValidator()
    {
        RuleFor(q => q.Slug).NotEmpty();
    }
}

internal sealed class PostChatMessageCommandHandler(ChatService service)
    : ICommandHandler<PostChatMessageCommand, ChatPostOutcome>
{
    public Task<ChatPostOutcome> HandleAsync(
        PostChatMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.PostAsync(
            command.TruppId, command.TeamId, command.AccountId, command.Body, command.PublishAt,
            cancellationToken);
    }
}

internal sealed class DeleteChatMessageCommandHandler(ChatService service)
    : ICommandHandler<DeleteChatMessageCommand, ChatModerationOutcome>
{
    public Task<ChatModerationOutcome> HandleAsync(
        DeleteChatMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DeleteAsync(
            command.TruppId, command.TeamId, command.MessageId, command.AccountId, cancellationToken);
    }
}

internal sealed class CancelScheduledChatMessageCommandHandler(ChatService service)
    : ICommandHandler<CancelScheduledChatMessageCommand, ChatModerationOutcome>
{
    public Task<ChatModerationOutcome> HandleAsync(
        CancelScheduledChatMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CancelScheduledAsync(
            command.TruppId, command.TeamId, command.MessageId, command.AccountId,
            command.ActorIsAdmin, cancellationToken);
    }
}

internal sealed class ReportChatMessageCommandHandler(ChatService service)
    : ICommandHandler<ReportChatMessageCommand, ChatModerationOutcome>
{
    public Task<ChatModerationOutcome> HandleAsync(
        ReportChatMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.ReportAsync(
            command.TruppId, command.TeamId, command.MessageId, command.AccountId, command.Reason,
            cancellationToken);
    }
}

internal sealed class AdminDeleteChatMessageCommandValidator
    : AbstractValidator<AdminDeleteChatMessageCommand>
{
    public AdminDeleteChatMessageCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.MessageId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();
    }
}

internal sealed class AdminDeleteChatMessageCommandHandler(ChatService service)
    : ICommandHandler<AdminDeleteChatMessageCommand, ChatModerationOutcome>
{
    public Task<ChatModerationOutcome> HandleAsync(
        AdminDeleteChatMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DeleteByAdminAsync(
            command.TruppId, command.MessageId, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class GetChatMessagesQueryHandler(ChatService service)
    : IQueryHandler<GetChatMessagesQuery, IReadOnlyList<ChatMessageDto>>
{
    public Task<IReadOnlyList<ChatMessageDto>> HandleAsync(
        GetChatMessagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListAsync(query.TruppId, query.TeamId, cancellationToken);
    }
}

internal sealed class GetScheduledChatMessagesQueryHandler(ChatService service)
    : IQueryHandler<GetScheduledChatMessagesQuery, IReadOnlyList<ScheduledMessageDto>>
{
    public Task<IReadOnlyList<ScheduledMessageDto>> HandleAsync(
        GetScheduledChatMessagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListScheduledAsync(
            query.TruppId, query.TeamId, query.AccountId, cancellationToken);
    }
}

internal sealed class GetReportedChatMessagesQueryHandler(ChatService service)
    : IQueryHandler<GetReportedChatMessagesQuery, IReadOnlyList<ReportedMessageDto>>
{
    public Task<IReadOnlyList<ReportedMessageDto>> HandleAsync(
        GetReportedChatMessagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListReportedAsync(query.TruppId, cancellationToken);
    }
}

internal sealed class GetChatChannelsQueryHandler(IMembershipService membership)
    : IQueryHandler<GetChatChannelsQuery, IReadOnlyList<ChatChannelDto>>
{
    public async Task<IReadOnlyList<ChatChannelDto>> HandleAsync(
        GetChatChannelsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var teams = await membership
            .AccessibleTeamChannelsAsync(query.AccountId, query.TruppId, cancellationToken)
            .ConfigureAwait(false);

        // Primärkanalen först, sedan lag-kanalerna (redan namnordnade av tjänsten).
        var channels = new List<ChatChannelDto>(teams.Count + 1) { ChatChannelDto.Trupp() };
        channels.AddRange(teams.Select(
            t => ChatChannelDto.Team(t.TeamId, t.Slug, t.Name, t.ColorHex)));

        return channels;
    }
}

internal sealed class GetTeamChannelQueryHandler(IChatRepository chat)
    : IQueryHandler<GetTeamChannelQuery, TeamChannel?>
{
    public Task<TeamChannel?> HandleAsync(
        GetTeamChannelQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return chat.FindTeamChannelAsync(query.Slug, cancellationToken);
    }
}

internal sealed class GetTeamChatMetaQueryHandler(
    IChatRepository chat, IMembershipService membership)
    : IQueryHandler<GetTeamChatMetaQuery, TeamChatMetaDto?>
{
    public async Task<TeamChatMetaDto?> HandleAsync(
        GetTeamChatMetaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var channel = await chat.FindTeamChannelAsync(query.Slug, cancellationToken)
            .ConfigureAwait(false);

        if (channel is null)
        {
            return null;
        }

        var isLeader = await membership
            .IsLeaderOfTruppAsync(query.AccountId, channel.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);

        return new TeamChatMetaDto(channel.AgeGroupId, isLeader);
    }
}
