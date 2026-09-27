using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Kontots notisinställning — en enda global på/av (`#332`-uppföljning, ersätter per-typ `#65`).
///
/// <para>
/// Av = kontot får ingen push; kritiska besked (kallelse, inställd/flyttad match) når ändå
/// fram via mejl. På = notiser som vanligt. En växel per konto, inte per lag och inte per typ.
/// </para>
/// </summary>
public sealed record NotificationSettingsDto(bool Enabled);

/// <summary>Kontots globala notisinställning.</summary>
public sealed record GetNotificationSettingsQuery(Guid AccountId) : IQuery<NotificationSettingsDto?>;

/// <summary>Sätter kontots globala på/av för notiser.</summary>
public sealed record SetNotificationSettingsCommand(Guid AccountId, bool Enabled)
    : ICommand<NotificationSettingsDto?>;

internal sealed class GetNotificationSettingsQueryValidator
    : AbstractValidator<GetNotificationSettingsQuery>
{
    public GetNotificationSettingsQueryValidator() => RuleFor(q => q.AccountId).NotEmpty();
}

internal sealed class SetNotificationSettingsCommandValidator
    : AbstractValidator<SetNotificationSettingsCommand>
{
    public SetNotificationSettingsCommandValidator() => RuleFor(c => c.AccountId).NotEmpty();
}

internal sealed class GetNotificationSettingsQueryHandler(NotificationSettingsService service)
    : IQueryHandler<GetNotificationSettingsQuery, NotificationSettingsDto?>
{
    public Task<NotificationSettingsDto?> HandleAsync(
        GetNotificationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.GetAsync(query.AccountId, cancellationToken);
    }
}

internal sealed class SetNotificationSettingsCommandHandler(NotificationSettingsService service)
    : ICommandHandler<SetNotificationSettingsCommand, NotificationSettingsDto?>
{
    public Task<NotificationSettingsDto?> HandleAsync(
        SetNotificationSettingsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.SetAsync(command.AccountId, command.Enabled, cancellationToken);
    }
}
