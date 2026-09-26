using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Events.Admin;

/*
 * Kommandona ar tunna omslag runt EventAdminService.
 *
 * Varfor bada delarna finns: tjansten haller reglerna som maste galla for varje andring
 * -- sekvensnumret, audit-posten, lagkontrollen -- och kommandona ger dem validering och
 * en gemensam vag in genom dispatchern. Utan kommandona hade varje controller behovt
 * komma ihag att validera; utan tjansten hade reglerna kopierats mellan fyra handlers.
 */

/// <summary>Lägger upp en händelse i laget som adressen pekar ut.</summary>
public sealed record CreateEventCommand(string TeamSlug, EventDraft Draft, Guid ActorAccountId)
    : ICommand<EventSaveResult>;

/// <summary>Ändrar en händelse.</summary>
public sealed record UpdateEventCommand(
    string TeamSlug,
    Guid EventId,
    EventDraft Draft,
    Guid ActorAccountId) : ICommand<EventSaveResult>;

/// <summary>Ställer in en händelse — kalenderposten blir kvar, markerad som inställd.</summary>
public sealed record CancelEventCommand(string TeamSlug, Guid EventId, Guid ActorAccountId)
    : ICommand<EventDto?>;

/// <summary>Tar bort en händelse som aldrig skulle ha lagts in.</summary>
public sealed record DeleteEventCommand(string TeamSlug, Guid EventId, Guid ActorAccountId)
    : ICommand<bool>;

internal sealed class CreateEventCommandHandler(EventAdminService service)
    : ICommandHandler<CreateEventCommand, EventSaveResult>
{
    public Task<EventSaveResult> HandleAsync(CreateEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.TeamSlug, command.Draft, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class UpdateEventCommandHandler(EventAdminService service)
    : ICommandHandler<UpdateEventCommand, EventSaveResult>
{
    public Task<EventSaveResult> HandleAsync(UpdateEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UpdateAsync(
            command.TeamSlug, command.EventId, command.Draft, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class CancelEventCommandHandler(EventAdminService service)
    : ICommandHandler<CancelEventCommand, EventDto?>
{
    public Task<EventDto?> HandleAsync(CancelEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CancelAsync(
            command.TeamSlug, command.EventId, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class DeleteEventCommandHandler(EventAdminService service)
    : ICommandHandler<DeleteEventCommand, bool>
{
    public Task<bool> HandleAsync(DeleteEventCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DeleteAsync(
            command.TeamSlug, command.EventId, command.ActorAccountId, cancellationToken);
    }
}
