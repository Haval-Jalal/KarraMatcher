using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Cup;

/*
 * Tunna omslag runt CupSignupService, av samma skäl som resten: tjänsten håller reglerna,
 * kommandona ger dem validering och en gemensam väg in genom dispatchern.
 */

/// <summary>Tränaren öppnar (eller ändrar) cupens platstak.</summary>
public sealed record OpenCupCommand(Guid TruppId, Guid EventId, int Capacity, Guid ActorAccountId)
    : ICommand<OpenCupOutcome>;

/// <summary>En vårdnadshavare anmäler sitt barn till cupen.</summary>
public sealed record SignUpForCupCommand(Guid EventId, Guid ChildId, Guid AccountId)
    : ICommand<CupSignupOutcome>;

/// <summary>Drar tillbaka en anmälan.</summary>
public sealed record WithdrawCupSignupCommand(Guid EventId, Guid ChildId, Guid AccountId)
    : ICommand<CupWithdrawOutcome>;

/// <summary>Cupens anmälningsläge för en truppmedlem.</summary>
public sealed record GetCupSummaryQuery(Guid EventId, Guid AccountId) : IQuery<CupSummaryDto?>;

internal sealed class OpenCupCommandValidator : AbstractValidator<OpenCupCommand>
{
    /// <summary>Övre gräns på platser — en rimlig cup-trupp, inte en oändlig siffra.</summary>
    public const int MaxCapacity = 100;

    public OpenCupCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();
        RuleFor(c => c.Capacity)
            .GreaterThan(0).WithMessage("Ange minst en plats.")
            .LessThanOrEqualTo(MaxCapacity).WithMessage("Det där är för många platser.");
    }
}

internal sealed class SignUpForCupCommandValidator : AbstractValidator<SignUpForCupCommand>
{
    public SignUpForCupCommandValidator()
    {
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.ChildId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
    }
}

internal sealed class WithdrawCupSignupCommandValidator : AbstractValidator<WithdrawCupSignupCommand>
{
    public WithdrawCupSignupCommandValidator()
    {
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.ChildId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
    }
}

internal sealed class OpenCupCommandHandler(CupSignupService service)
    : ICommandHandler<OpenCupCommand, OpenCupOutcome>
{
    public Task<OpenCupOutcome> HandleAsync(OpenCupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.OpenAsync(
            command.TruppId, command.EventId, command.Capacity, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class SignUpForCupCommandHandler(CupSignupService service)
    : ICommandHandler<SignUpForCupCommand, CupSignupOutcome>
{
    public Task<CupSignupOutcome> HandleAsync(
        SignUpForCupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.SignUpAsync(command.EventId, command.ChildId, command.AccountId, cancellationToken);
    }
}

internal sealed class WithdrawCupSignupCommandHandler(CupSignupService service)
    : ICommandHandler<WithdrawCupSignupCommand, CupWithdrawOutcome>
{
    public Task<CupWithdrawOutcome> HandleAsync(
        WithdrawCupSignupCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.WithdrawAsync(command.EventId, command.ChildId, command.AccountId, cancellationToken);
    }
}

internal sealed class GetCupSummaryQueryHandler(CupSignupService service)
    : IQueryHandler<GetCupSummaryQuery, CupSummaryDto?>
{
    public Task<CupSummaryDto?> HandleAsync(GetCupSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.SummaryAsync(query.EventId, query.AccountId, cancellationToken);
    }
}
