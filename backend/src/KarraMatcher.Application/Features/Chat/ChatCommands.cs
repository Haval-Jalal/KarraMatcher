using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Chat;

/// <summary>Postar (eller schemalägger) ett meddelande i trupp-chatten.</summary>
public sealed record PostChatMessageCommand(
    Guid TruppId,
    Guid AccountId,
    string Body,
    DateTimeOffset? PublishAt) : ICommand<ChatPostOutcome>;

/// <summary>Tar bort ett meddelande (eget, eller vilket som helst om anroparen är admin).</summary>
public sealed record DeleteChatMessageCommand(
    Guid TruppId,
    Guid MessageId,
    Guid AccountId,
    bool ActorIsAdmin) : ICommand<ChatModerationOutcome>;

/// <summary>Avbokar ett schemalagt meddelande innan det går ut.</summary>
public sealed record CancelScheduledChatMessageCommand(
    Guid TruppId,
    Guid MessageId,
    Guid AccountId,
    bool ActorIsAdmin) : ICommand<ChatModerationOutcome>;

/// <summary>Anmäler ett meddelande.</summary>
public sealed record ReportChatMessageCommand(
    Guid TruppId,
    Guid MessageId,
    Guid AccountId) : ICommand<ChatModerationOutcome>;

/// <summary>De senaste meddelandena i trupp-chatten.</summary>
public sealed record GetChatMessagesQuery(Guid TruppId) : IQuery<IReadOnlyList<ChatMessageDto>>;

/// <summary>Den inloggades egna schemalagda meddelanden.</summary>
public sealed record GetScheduledChatMessagesQuery(Guid TruppId, Guid AccountId)
    : IQuery<IReadOnlyList<ScheduledMessageDto>>;

/// <summary>Adminens kö av anmälda meddelanden.</summary>
public sealed record GetReportedChatMessagesQuery(Guid TruppId)
    : IQuery<IReadOnlyList<ReportedMessageDto>>;

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

internal sealed class PostChatMessageCommandHandler(ChatService service)
    : ICommandHandler<PostChatMessageCommand, ChatPostOutcome>
{
    public Task<ChatPostOutcome> HandleAsync(
        PostChatMessageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.PostAsync(
            command.TruppId, command.AccountId, command.Body, command.PublishAt, cancellationToken);
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
            command.TruppId, command.MessageId, command.AccountId, command.ActorIsAdmin,
            cancellationToken);
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
            command.TruppId, command.MessageId, command.AccountId, command.ActorIsAdmin,
            cancellationToken);
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
            command.TruppId, command.MessageId, command.AccountId, cancellationToken);
    }
}

internal sealed class GetChatMessagesQueryHandler(ChatService service)
    : IQueryHandler<GetChatMessagesQuery, IReadOnlyList<ChatMessageDto>>
{
    public Task<IReadOnlyList<ChatMessageDto>> HandleAsync(
        GetChatMessagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListAsync(query.TruppId, cancellationToken);
    }
}

internal sealed class GetScheduledChatMessagesQueryHandler(ChatService service)
    : IQueryHandler<GetScheduledChatMessagesQuery, IReadOnlyList<ScheduledMessageDto>>
{
    public Task<IReadOnlyList<ScheduledMessageDto>> HandleAsync(
        GetScheduledChatMessagesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListScheduledAsync(query.TruppId, query.AccountId, cancellationToken);
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
