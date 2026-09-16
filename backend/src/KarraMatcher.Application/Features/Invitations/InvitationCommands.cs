using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

namespace KarraMatcher.Application.Features.Invitations;

/// <summary>Skapar en inbjudan till en trupp och mejlar länken (`#193`).</summary>
public sealed record CreateInvitationCommand(
    Guid AgeGroupId, string Email, Guid? TeamId, Guid ActorAccountId)
    : ICommand<AdminResult<InvitationCreatedDto>>;

/// <summary>Accepterar en inbjudan — skapar förälderns medlemskap.</summary>
public sealed record AcceptInvitationCommand(string Token, Guid AccountId)
    : ICommand<InvitationAcceptResult>;

/// <summary>Återkallar en väntande inbjudan i en trupp.</summary>
public sealed record RevokeInvitationCommand(Guid AgeGroupId, Guid Id, Guid ActorAccountId)
    : ICommand<AdminOutcome>;

internal sealed class CreateInvitationCommandHandler(InvitationService service)
    : ICommandHandler<CreateInvitationCommand, AdminResult<InvitationCreatedDto>>
{
    public Task<AdminResult<InvitationCreatedDto>> HandleAsync(
        CreateInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.AgeGroupId, command.Email, command.TeamId, command.ActorAccountId,
            cancellationToken);
    }
}

internal sealed class AcceptInvitationCommandHandler(InvitationService service)
    : ICommandHandler<AcceptInvitationCommand, InvitationAcceptResult>
{
    public Task<InvitationAcceptResult> HandleAsync(
        AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.AcceptAsync(command.Token, command.AccountId, cancellationToken);
    }
}

internal sealed class RevokeInvitationCommandHandler(InvitationService service)
    : ICommandHandler<RevokeInvitationCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RevokeAsync(
            command.AgeGroupId, command.Id, command.ActorAccountId, cancellationToken);
    }
}
