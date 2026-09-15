using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Domain.Attendance;

namespace KarraMatcher.Application.Features.Attendance;

/// <summary>Tränaren kallar till en match. Laget står i adressen, inte i kroppen.</summary>
public sealed record OpenAttendanceCallCommand(string Slug, Guid MatchId, Guid ActorAccountId)
    : ICommand<OpenCallOutcome>;

/// <summary>En vuxen svarar för sin familj: status och antal, aldrig ett barn.</summary>
public sealed record SubmitAttendanceResponseCommand(
    Guid MatchId,
    Guid AccountId,
    AttendanceStatus Status,
    int Count) : ICommand<SubmitResponseOutcome>;

/// <summary>Ett konts eget läge på en match: öppen kallelse, avspark, eget svar.</summary>
public sealed record GetAttendanceStateQuery(Guid MatchId, Guid AccountId)
    : IQuery<AttendanceStateDto?>;

/// <summary>Tränarens summering för en match. Laget står i adressen (CoachOfTeam).</summary>
public sealed record GetAttendanceSummaryQuery(string Slug, Guid MatchId)
    : IQuery<AttendanceSummaryDto?>;

/// <summary>Påminner dem som inte svarat. Ger antalet, eller null när matchen inte hör till laget.</summary>
public sealed record RemindNonRespondersCommand(string Slug, Guid MatchId) : ICommand<int?>;

internal sealed class OpenAttendanceCallCommandValidator
    : AbstractValidator<OpenAttendanceCallCommand>
{
    public OpenAttendanceCallCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty();
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();
    }
}

internal sealed class SubmitAttendanceResponseCommandValidator
    : AbstractValidator<SubmitAttendanceResponseCommand>
{
    /// <summary>Fyra ur en familj till en match. Gränsen prövas här, inte bara i formuläret.</summary>
    internal const int MaxCount = 4;

    public SubmitAttendanceResponseCommandValidator()
    {
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();

        RuleFor(c => c.Status)
            .IsInEnum().WithMessage("Välj Kommer, Kan inte eller Kanske.");

        RuleFor(c => c.Count)
            .InclusiveBetween(0, MaxCount)
            .WithMessage($"Antalet måste vara mellan 0 och {MaxCount}.");

        /*
         * Ett "kommer" utan nagon som kommer ar inget svar. "Kan inte" tvingas till noll i
         * tjansten, och "kanske" far vara noll -- den som inte vet hur manga anger det den
         * tror. Bara "kommer" kraver minst en.
         */
        RuleFor(c => c.Count)
            .GreaterThanOrEqualTo(1)
            .When(c => c.Status == AttendanceStatus.Coming)
            .WithMessage("Ange hur många som kommer.");
    }
}

internal sealed class OpenAttendanceCallCommandHandler(AttendanceService service)
    : ICommandHandler<OpenAttendanceCallCommand, OpenCallOutcome>
{
    public Task<OpenCallOutcome> HandleAsync(
        OpenAttendanceCallCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.OpenCallAsync(
            command.Slug, command.MatchId, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class SubmitAttendanceResponseCommandHandler(AttendanceService service)
    : ICommandHandler<SubmitAttendanceResponseCommand, SubmitResponseOutcome>
{
    public Task<SubmitResponseOutcome> HandleAsync(
        SubmitAttendanceResponseCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.SubmitResponseAsync(
            command.MatchId, command.AccountId, command.Status, command.Count, cancellationToken);
    }
}

internal sealed class GetAttendanceStateQueryHandler(AttendanceService service)
    : IQueryHandler<GetAttendanceStateQuery, AttendanceStateDto?>
{
    public Task<AttendanceStateDto?> HandleAsync(
        GetAttendanceStateQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.GetStateAsync(query.MatchId, query.AccountId, cancellationToken);
    }
}

internal sealed class GetAttendanceSummaryQueryValidator
    : AbstractValidator<GetAttendanceSummaryQuery>
{
    public GetAttendanceSummaryQueryValidator()
    {
        RuleFor(q => q.Slug).NotEmpty();
        RuleFor(q => q.MatchId).NotEmpty();
    }
}

internal sealed class GetAttendanceSummaryQueryHandler(AttendanceService service)
    : IQueryHandler<GetAttendanceSummaryQuery, AttendanceSummaryDto?>
{
    public Task<AttendanceSummaryDto?> HandleAsync(
        GetAttendanceSummaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.GetSummaryAsync(query.Slug, query.MatchId, cancellationToken);
    }
}

internal sealed class RemindNonRespondersCommandValidator
    : AbstractValidator<RemindNonRespondersCommand>
{
    public RemindNonRespondersCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty();
        RuleFor(c => c.MatchId).NotEmpty();
    }
}

internal sealed class RemindNonRespondersCommandHandler(AttendanceService service)
    : ICommandHandler<RemindNonRespondersCommand, int?>
{
    public Task<int?> HandleAsync(
        RemindNonRespondersCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RemindNonRespondersAsync(command.Slug, command.MatchId, cancellationToken);
    }
}
