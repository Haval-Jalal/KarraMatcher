using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Skapar en trupp. Tunt omslag runt <see cref="TruppAdminService"/> (`#192`).</summary>
public sealed record CreateTruppCommand(
    Guid ClubId, Guid SportId, string Name, Guid ActorAccountId)
    : ICommand<AdminResult<TruppDto>>;

/// <summary>Ändrar en trupps sport och namn (säsong utgått, `#261`).</summary>
public sealed record UpdateTruppCommand(
    Guid Id, Guid SportId, string Name, Guid ActorAccountId)
    : ICommand<AdminResult<TruppDto>>;

internal sealed class CreateTruppCommandHandler(TruppAdminService service)
    : ICommandHandler<CreateTruppCommand, AdminResult<TruppDto>>
{
    public Task<AdminResult<TruppDto>> HandleAsync(
        CreateTruppCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.ClubId, command.SportId, command.Name,
            command.ActorAccountId, cancellationToken);
    }
}

internal sealed class UpdateTruppCommandHandler(TruppAdminService service)
    : ICommandHandler<UpdateTruppCommand, AdminResult<TruppDto>>
{
    public Task<AdminResult<TruppDto>> HandleAsync(
        UpdateTruppCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UpdateAsync(
            command.Id, command.SportId, command.Name,
            command.ActorAccountId, cancellationToken);
    }
}
