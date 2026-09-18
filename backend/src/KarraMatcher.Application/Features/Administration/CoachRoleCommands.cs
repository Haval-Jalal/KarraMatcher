using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Tillsätter en tränare för ett lag via kontots adress (`#197`).</summary>
public sealed record GrantCoachCommand(Guid TruppId, Guid TeamId, string Email, Guid ActorAccountId)
    : ICommand<AdminResult<TeamCoachDto>>;

/// <summary>Avsätter en tränare från ett lag.</summary>
public sealed record RevokeCoachCommand(Guid TruppId, Guid TeamId, Guid AccountId, Guid ActorAccountId)
    : ICommand<AdminOutcome>;

internal sealed class GrantCoachCommandHandler(CoachRoleService service)
    : ICommandHandler<GrantCoachCommand, AdminResult<TeamCoachDto>>
{
    public Task<AdminResult<TeamCoachDto>> HandleAsync(
        GrantCoachCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.GrantAsync(
            command.TruppId, command.TeamId, command.Email, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class RevokeCoachCommandHandler(CoachRoleService service)
    : ICommandHandler<RevokeCoachCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        RevokeCoachCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RevokeAsync(
            command.TruppId, command.TeamId, command.AccountId, command.ActorAccountId, cancellationToken);
    }
}
