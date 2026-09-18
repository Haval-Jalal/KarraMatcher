using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

namespace KarraMatcher.Application.Features.Children;

/// <summary>Skapar ett barn i en trupp, valfritt direkt i ett lag (`#196`).</summary>
public sealed record CreateChildCommand(
    Guid AgeGroupId, string FirstName, string LastInitial, Guid? TeamId, Guid ActorAccountId)
    : ICommand<AdminResult<ChildDto>>;

/// <summary>Ändrar ett barns namn och lag (flytta = ändra lag, otilldelad = null).</summary>
public sealed record UpdateChildCommand(
    Guid AgeGroupId, Guid Id, string FirstName, string LastInitial, Guid? TeamId, Guid ActorAccountId)
    : ICommand<AdminResult<ChildDto>>;

/// <summary>Tar bort ett barn ur truppen.</summary>
public sealed record DeleteChildCommand(Guid AgeGroupId, Guid Id, Guid ActorAccountId)
    : ICommand<AdminOutcome>;

/// <summary>Kopplar en vårdnadshavare till ett barn (kräver samtycke, §KM.6).</summary>
public sealed record LinkGuardianCommand(
    Guid AgeGroupId, Guid ChildId, string Email, Guid ActorAccountId)
    : ICommand<LinkGuardianOutcome>;

/// <summary>Kopplar bort en vårdnadshavare från ett barn.</summary>
public sealed record UnlinkGuardianCommand(
    Guid AgeGroupId, Guid ChildId, Guid AccountId, Guid ActorAccountId)
    : ICommand<AdminOutcome>;

internal sealed class CreateChildCommandHandler(ChildAdminService service)
    : ICommandHandler<CreateChildCommand, AdminResult<ChildDto>>
{
    public Task<AdminResult<ChildDto>> HandleAsync(
        CreateChildCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.AgeGroupId, command.FirstName, command.LastInitial, command.TeamId,
            command.ActorAccountId, cancellationToken);
    }
}

internal sealed class UpdateChildCommandHandler(ChildAdminService service)
    : ICommandHandler<UpdateChildCommand, AdminResult<ChildDto>>
{
    public Task<AdminResult<ChildDto>> HandleAsync(
        UpdateChildCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UpdateAsync(
            command.AgeGroupId, command.Id, command.FirstName, command.LastInitial, command.TeamId,
            command.ActorAccountId, cancellationToken);
    }
}

internal sealed class DeleteChildCommandHandler(ChildAdminService service)
    : ICommandHandler<DeleteChildCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        DeleteChildCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DeleteAsync(
            command.AgeGroupId, command.Id, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class LinkGuardianCommandHandler(ChildAdminService service)
    : ICommandHandler<LinkGuardianCommand, LinkGuardianOutcome>
{
    public Task<LinkGuardianOutcome> HandleAsync(
        LinkGuardianCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.LinkGuardianAsync(
            command.AgeGroupId, command.ChildId, command.Email, command.ActorAccountId,
            cancellationToken);
    }
}

internal sealed class UnlinkGuardianCommandHandler(ChildAdminService service)
    : ICommandHandler<UnlinkGuardianCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        UnlinkGuardianCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UnlinkGuardianAsync(
            command.AgeGroupId, command.ChildId, command.AccountId, command.ActorAccountId,
            cancellationToken);
    }
}
