using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Domain.Attendance;

namespace KarraMatcher.Application.Features.Attendance;

/// <summary>
/// Admin skickar/uppdaterar kallelsen: vilka barn ur truppen som kallas till händelsen.
/// Truppen och händelsen står i adressen, barnen i kroppen (§KM.7, `#199`).
/// </summary>
public sealed record SetKallelseCommand(
    Guid TruppId,
    Guid EventId,
    IReadOnlyList<Guid> ChildIds,
    Guid ActorAccountId) : ICommand<SetKallelseOutcome>;

/// <summary>En vårdnadshavare svarar Ja/Nej för ett av sina barn.</summary>
public sealed record RespondToKallelseCommand(
    Guid EventId,
    Guid ChildId,
    Guid AccountId,
    AttendanceReply Reply) : ICommand<RespondOutcome>;

/// <summary>Den inloggades egna kallade barn för en händelse.</summary>
public sealed record GetMyKallelseQuery(Guid EventId, Guid AccountId) : IQuery<MyKallelseDto?>;

/// <summary>Adminens sammanställning för en händelse. Truppen står i adressen (AdminOfTrupp).</summary>
public sealed record GetKallelseSummaryQuery(Guid TruppId, Guid EventId) : IQuery<KallelseSummaryDto?>;

/// <summary>Påminner dem som inte svarat. Antalet, eller null när händelsen inte hör till truppen.</summary>
public sealed record RemindNonRespondersCommand(Guid TruppId, Guid EventId) : ICommand<int?>;

internal sealed class SetKallelseCommandValidator : AbstractValidator<SetKallelseCommand>
{
    public SetKallelseCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();
        RuleFor(c => c.ChildIds).NotNull();
    }
}

internal sealed class RespondToKallelseCommandValidator
    : AbstractValidator<RespondToKallelseCommand>
{
    public RespondToKallelseCommandValidator()
    {
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.ChildId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
        RuleFor(c => c.Reply).IsInEnum().WithMessage("Svara Ja eller Nej.");
    }
}

internal sealed class GetKallelseSummaryQueryValidator : AbstractValidator<GetKallelseSummaryQuery>
{
    public GetKallelseSummaryQueryValidator()
    {
        RuleFor(q => q.TruppId).NotEmpty();
        RuleFor(q => q.EventId).NotEmpty();
    }
}

internal sealed class RemindNonRespondersCommandValidator
    : AbstractValidator<RemindNonRespondersCommand>
{
    public RemindNonRespondersCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.EventId).NotEmpty();
    }
}

internal sealed class SetKallelseCommandHandler(AttendanceService service)
    : ICommandHandler<SetKallelseCommand, SetKallelseOutcome>
{
    public Task<SetKallelseOutcome> HandleAsync(
        SetKallelseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.SetKallelseAsync(
            command.TruppId, command.EventId, command.ChildIds, command.ActorAccountId,
            cancellationToken);
    }
}

internal sealed class RespondToKallelseCommandHandler(AttendanceService service)
    : ICommandHandler<RespondToKallelseCommand, RespondOutcome>
{
    public Task<RespondOutcome> HandleAsync(
        RespondToKallelseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RespondAsync(
            command.EventId, command.ChildId, command.AccountId, command.Reply, cancellationToken);
    }
}

internal sealed class GetMyKallelseQueryHandler(AttendanceService service)
    : IQueryHandler<GetMyKallelseQuery, MyKallelseDto?>
{
    public Task<MyKallelseDto?> HandleAsync(
        GetMyKallelseQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.GetMineAsync(query.EventId, query.AccountId, cancellationToken);
    }
}

internal sealed class GetKallelseSummaryQueryHandler(AttendanceService service)
    : IQueryHandler<GetKallelseSummaryQuery, KallelseSummaryDto?>
{
    public Task<KallelseSummaryDto?> HandleAsync(
        GetKallelseSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.GetSummaryAsync(query.TruppId, query.EventId, cancellationToken);
    }
}

internal sealed class RemindNonRespondersCommandHandler(AttendanceService service)
    : ICommandHandler<RemindNonRespondersCommand, int?>
{
    public Task<int?> HandleAsync(
        RemindNonRespondersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RemindNonRespondersAsync(command.TruppId, command.EventId, cancellationToken);
    }
}
