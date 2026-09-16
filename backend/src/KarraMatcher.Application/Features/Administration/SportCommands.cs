using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Skapar en sport. Tunt omslag runt <see cref="SportAdminService"/> (`#192`).</summary>
public sealed record CreateSportCommand(string Name, string Slug, Guid ActorAccountId)
    : ICommand<AdminResult<SportDto>>;

/// <summary>Ändrar en sports namn (slugen är stabil).</summary>
public sealed record UpdateSportCommand(Guid Id, string Name, Guid ActorAccountId)
    : ICommand<AdminResult<SportDto>>;

internal sealed class CreateSportCommandHandler(SportAdminService service)
    : ICommandHandler<CreateSportCommand, AdminResult<SportDto>>
{
    public Task<AdminResult<SportDto>> HandleAsync(
        CreateSportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(command.Name, command.Slug, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class UpdateSportCommandHandler(SportAdminService service)
    : ICommandHandler<UpdateSportCommand, AdminResult<SportDto>>
{
    public Task<AdminResult<SportDto>> HandleAsync(
        UpdateSportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UpdateAsync(command.Id, command.Name, command.ActorAccountId, cancellationToken);
    }
}
