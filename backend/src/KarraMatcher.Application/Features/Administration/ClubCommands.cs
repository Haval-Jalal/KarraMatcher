using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Skapar en klubb. Tunt omslag runt <see cref="ClubAdminService"/> (`#192`).</summary>
public sealed record CreateClubCommand(string Name, string Slug, Guid ActorAccountId)
    : ICommand<AdminResult<ClubDto>>;

/// <summary>Ändrar en klubbs namn (slugen är stabil).</summary>
public sealed record UpdateClubCommand(Guid Id, string Name, Guid ActorAccountId)
    : ICommand<AdminResult<ClubDto>>;

internal sealed class CreateClubCommandHandler(ClubAdminService service)
    : ICommandHandler<CreateClubCommand, AdminResult<ClubDto>>
{
    public Task<AdminResult<ClubDto>> HandleAsync(
        CreateClubCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(command.Name, command.Slug, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class UpdateClubCommandHandler(ClubAdminService service)
    : ICommandHandler<UpdateClubCommand, AdminResult<ClubDto>>
{
    public Task<AdminResult<ClubDto>> HandleAsync(
        UpdateClubCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UpdateAsync(command.Id, command.Name, command.ActorAccountId, cancellationToken);
    }
}
