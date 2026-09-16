using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

namespace KarraMatcher.Application.Features.Applications;

/// <summary>En förälder ansöker om att gå med i en trupp (`#194`).</summary>
public sealed record SubmitApplicationCommand(Guid AgeGroupId, Guid AccountId)
    : ICommand<AdminOutcome>;

/// <summary>Admin godkänner en ansökan — skapar förälderns medlemskap.</summary>
public sealed record ApproveApplicationCommand(Guid AgeGroupId, Guid Id, Guid ActorAccountId)
    : ICommand<AdminOutcome>;

/// <summary>Admin nekar en ansökan.</summary>
public sealed record DenyApplicationCommand(Guid AgeGroupId, Guid Id, Guid ActorAccountId)
    : ICommand<AdminOutcome>;

internal sealed class SubmitApplicationCommandHandler(ApplicationService service)
    : ICommandHandler<SubmitApplicationCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        SubmitApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.ApplyAsync(command.AgeGroupId, command.AccountId, cancellationToken);
    }
}

internal sealed class ApproveApplicationCommandHandler(ApplicationService service)
    : ICommandHandler<ApproveApplicationCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        ApproveApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.ApproveAsync(
            command.AgeGroupId, command.Id, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class DenyApplicationCommandHandler(ApplicationService service)
    : ICommandHandler<DenyApplicationCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        DenyApplicationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DenyAsync(
            command.AgeGroupId, command.Id, command.ActorAccountId, cancellationToken);
    }
}
