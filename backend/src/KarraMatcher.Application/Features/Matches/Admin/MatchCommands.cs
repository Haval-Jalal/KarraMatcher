using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Matches.Admin;

/*
 * Kommandona ar tunna omslag runt MatchAdminService.
 *
 * Varfor bada delarna finns: tjansten haller reglerna som maste galla for varje andring
 * -- sekvensnumret, audit-posten, lagkontrollen -- och kommandona ger dem validering och
 * en gemensam vag in genom dispatchern. Utan kommandona hade varje controller behovt
 * komma ihag att validera; utan tjansten hade reglerna kopierats mellan fyra handlers.
 */

/// <summary>Lägger upp en match i laget som adressen pekar ut.</summary>
public sealed record CreateMatchCommand(string TeamSlug, MatchDraft Draft, Guid ActorAccountId)
    : ICommand<MatchDto?>;

/// <summary>Ändrar en match.</summary>
public sealed record UpdateMatchCommand(
    string TeamSlug,
    Guid MatchId,
    MatchDraft Draft,
    Guid ActorAccountId) : ICommand<MatchDto?>;

/// <summary>Ställer in en match — kalenderposten blir kvar, markerad som inställd.</summary>
public sealed record CancelMatchCommand(string TeamSlug, Guid MatchId, Guid ActorAccountId)
    : ICommand<MatchDto?>;

/// <summary>Tar bort en match som aldrig skulle ha lagts in.</summary>
public sealed record DeleteMatchCommand(string TeamSlug, Guid MatchId, Guid ActorAccountId)
    : ICommand<bool>;

internal sealed class CreateMatchCommandHandler(MatchAdminService service)
    : ICommandHandler<CreateMatchCommand, MatchDto?>
{
    public Task<MatchDto?> HandleAsync(CreateMatchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.TeamSlug, command.Draft, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class UpdateMatchCommandHandler(MatchAdminService service)
    : ICommandHandler<UpdateMatchCommand, MatchDto?>
{
    public Task<MatchDto?> HandleAsync(UpdateMatchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UpdateAsync(
            command.TeamSlug, command.MatchId, command.Draft, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class CancelMatchCommandHandler(MatchAdminService service)
    : ICommandHandler<CancelMatchCommand, MatchDto?>
{
    public Task<MatchDto?> HandleAsync(CancelMatchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CancelAsync(
            command.TeamSlug, command.MatchId, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class DeleteMatchCommandHandler(MatchAdminService service)
    : ICommandHandler<DeleteMatchCommand, bool>
{
    public Task<bool> HandleAsync(DeleteMatchCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DeleteAsync(
            command.TeamSlug, command.MatchId, command.ActorAccountId, cancellationToken);
    }
}
