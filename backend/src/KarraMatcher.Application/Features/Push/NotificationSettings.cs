using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Domain.Push;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// En förälders notisval för ett lag, så som appen visar och sätter dem (`#65`).
///
/// <para>
/// En växel per notistyp: händelser, kallelser, samåkning och chatt. Allt på är förvalet —
/// den som aldrig rört inställningarna får alla. Chatt finns med redan nu (`#200`) fast
/// utskicket byggs i `#201`/`#202`.
/// </para>
/// </summary>
public sealed record NotificationSettingsDto(
    bool EventChanges, bool Kallelser, bool Carpool, bool Chat)
{
    public static NotificationSettingsDto For(NotificationPreference preference)
    {
        ArgumentNullException.ThrowIfNull(preference);

        return new NotificationSettingsDto(
            preference.EventChanges, preference.Kallelser, preference.Carpool, preference.Chat);
    }
}

/// <summary>Det den inloggade skickar in när hen ändrar sina val.</summary>
public sealed record NotificationSettingsDraft(
    bool EventChanges, bool Kallelser, bool Carpool, bool Chat);

/// <summary>Kontots notisval för ett lag. Laget står i adressen, kontot är den inloggade.</summary>
public sealed record GetNotificationSettingsQuery(string Slug, Guid AccountId)
    : IQuery<NotificationSettingsDto?>;

/// <summary>Sätter kontots notisval för ett lag.</summary>
public sealed record SetNotificationSettingsCommand(
    string Slug,
    Guid AccountId,
    NotificationSettingsDraft Draft) : ICommand<NotificationSettingsDto?>;

internal sealed class GetNotificationSettingsQueryValidator
    : AbstractValidator<GetNotificationSettingsQuery>
{
    public GetNotificationSettingsQueryValidator()
    {
        RuleFor(q => q.Slug).NotEmpty();
        RuleFor(q => q.AccountId).NotEmpty();
    }
}

internal sealed class SetNotificationSettingsCommandValidator
    : AbstractValidator<SetNotificationSettingsCommand>
{
    public SetNotificationSettingsCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
        RuleFor(c => c.Draft).NotNull();
    }
}

internal sealed class GetNotificationSettingsQueryHandler(NotificationSettingsService service)
    : IQueryHandler<GetNotificationSettingsQuery, NotificationSettingsDto?>
{
    public Task<NotificationSettingsDto?> HandleAsync(
        GetNotificationSettingsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.GetAsync(query.Slug, query.AccountId, cancellationToken);
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

        return service.SetAsync(command.Slug, command.AccountId, command.Draft, cancellationToken);
    }
}
