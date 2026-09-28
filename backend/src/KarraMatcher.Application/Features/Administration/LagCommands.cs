using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>Skapar ett lag. Tunt omslag runt <see cref="LagAdminService"/> (`#192`).</summary>
public sealed record CreateLagCommand(
    Guid TruppId, string Name, string ColorHex, string Slug, Guid ActorAccountId)
    : ICommand<AdminResult<LagDto>>;

/// <summary>
/// Ändrar ett lags namn och färg (slugen är stabil). <c>TruppId</c> bär truppen ur adressen så
/// tjänsten kan verifiera att laget verkligen hör dit — annars kunde en admin för en trupp ändra
/// ett lag i en annan (IDOR, §KM.3-objektnivå).
/// </summary>
public sealed record UpdateLagCommand(
    Guid TruppId, Guid Id, string Name, string ColorHex, Guid ActorAccountId)
    : ICommand<AdminResult<LagDto>>;

internal sealed class CreateLagCommandHandler(LagAdminService service)
    : ICommandHandler<CreateLagCommand, AdminResult<LagDto>>
{
    public Task<AdminResult<LagDto>> HandleAsync(
        CreateLagCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.TruppId, command.Name, command.ColorHex, command.Slug,
            command.ActorAccountId, cancellationToken);
    }
}

internal sealed class UpdateLagCommandHandler(LagAdminService service)
    : ICommandHandler<UpdateLagCommand, AdminResult<LagDto>>
{
    public Task<AdminResult<LagDto>> HandleAsync(
        UpdateLagCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UpdateAsync(
            command.TruppId, command.Id, command.Name, command.ColorHex, command.ActorAccountId,
            cancellationToken);
    }
}
