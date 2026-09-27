using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Cup;

/*
 * Cup-lags-kommandona (`#335`) är tunna omslag runt CupTeamService, precis som
 * händelsekommandona runt EventAdminService. Behörigheten (AdminOfTrupp) sitter på endpointen;
 * objektnivån (cupen hör till truppen, laget till cupen, barnet är anmält) i tjänsten.
 */

/// <summary>Skapar ett tomt cup-lag i cupen.</summary>
public sealed record CreateCupTeamCommand(Guid TruppId, Guid EventId, string Name)
    : ICommand<CupTeamCreateResult>;

/// <summary>Byter namn på ett cup-lag.</summary>
public sealed record RenameCupTeamCommand(Guid TruppId, Guid EventId, Guid CupTeamId, string Name)
    : ICommand<CupTeamOutcome>;

/// <summary>Tar bort ett cup-lag.</summary>
public sealed record DeleteCupTeamCommand(Guid TruppId, Guid EventId, Guid CupTeamId)
    : ICommand<CupTeamOutcome>;

/// <summary>Placerar ett anmält barn i ett cup-lag (flyttar om det redan står i ett annat).</summary>
public sealed record AssignCupChildCommand(Guid TruppId, Guid EventId, Guid CupTeamId, Guid ChildId)
    : ICommand<CupTeamOutcome>;

/// <summary>Tar bort ett barn ur ett cup-lag.</summary>
public sealed record UnassignCupChildCommand(Guid TruppId, Guid EventId, Guid CupTeamId, Guid ChildId)
    : ICommand<CupTeamOutcome>;

internal sealed class CreateCupTeamCommandHandler(CupTeamService service)
    : ICommandHandler<CreateCupTeamCommand, CupTeamCreateResult>
{
    public Task<CupTeamCreateResult> HandleAsync(
        CreateCupTeamCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateTeamAsync(
            command.TruppId, command.EventId, command.Name, cancellationToken);
    }
}

internal sealed class RenameCupTeamCommandHandler(CupTeamService service)
    : ICommandHandler<RenameCupTeamCommand, CupTeamOutcome>
{
    public Task<CupTeamOutcome> HandleAsync(
        RenameCupTeamCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RenameTeamAsync(
            command.TruppId, command.EventId, command.CupTeamId, command.Name, cancellationToken);
    }
}

internal sealed class DeleteCupTeamCommandHandler(CupTeamService service)
    : ICommandHandler<DeleteCupTeamCommand, CupTeamOutcome>
{
    public Task<CupTeamOutcome> HandleAsync(
        DeleteCupTeamCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DeleteTeamAsync(
            command.TruppId, command.EventId, command.CupTeamId, cancellationToken);
    }
}

internal sealed class AssignCupChildCommandHandler(CupTeamService service)
    : ICommandHandler<AssignCupChildCommand, CupTeamOutcome>
{
    public Task<CupTeamOutcome> HandleAsync(
        AssignCupChildCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.AssignChildAsync(
            command.TruppId, command.EventId, command.CupTeamId, command.ChildId, cancellationToken);
    }
}

internal sealed class UnassignCupChildCommandHandler(CupTeamService service)
    : ICommandHandler<UnassignCupChildCommand, CupTeamOutcome>
{
    public Task<CupTeamOutcome> HandleAsync(
        UnassignCupChildCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.UnassignChildAsync(
            command.TruppId, command.EventId, command.CupTeamId, command.ChildId, cancellationToken);
    }
}
