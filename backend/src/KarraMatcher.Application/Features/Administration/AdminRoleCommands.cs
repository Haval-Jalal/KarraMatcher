using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Tilldelar en admin till en trupp via kontots adress (`#192`).</summary>
public sealed record GrantAdminCommand(Guid TruppId, string Email, Guid ActorAccountId)
    : ICommand<AdminResult<TruppAdminDto>>;

/// <summary>Återkallar en admins roll för en trupp.</summary>
public sealed record RevokeAdminCommand(Guid TruppId, Guid AccountId, Guid ActorAccountId)
    : ICommand<AdminOutcome>;

internal sealed class GrantAdminCommandHandler(AdminRoleService service)
    : ICommandHandler<GrantAdminCommand, AdminResult<TruppAdminDto>>
{
    public Task<AdminResult<TruppAdminDto>> HandleAsync(
        GrantAdminCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.GrantAsync(
            command.TruppId, command.Email, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class RevokeAdminCommandHandler(AdminRoleService service)
    : ICommandHandler<RevokeAdminCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        RevokeAdminCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RevokeAsync(
            command.TruppId, command.AccountId, command.ActorAccountId, cancellationToken);
    }
}
