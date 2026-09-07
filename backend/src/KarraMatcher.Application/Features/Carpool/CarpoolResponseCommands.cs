using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>Föraren accepterar. Meddelandet är valfritt — ett ja behöver inga ord.</summary>
public sealed record AcceptCarpoolRequestCommand(
    Guid RequestId,
    string? Message,
    Guid ActorAccountId) : ICommand<(CarpoolResponseOutcome Outcome, int SeatsLeft)>;

/// <summary>
/// Föraren nekar. Meddelandet är obligatoriskt (§KM.12).
///
/// <para>
/// Att kravet ligger i typen och inte bara i valideringen är avsiktligt: ett nekande utan ord
/// ska inte gå att uttrycka, inte ens av kod som skrivs långt senare.
/// </para>
/// </summary>
public sealed record DenyCarpoolRequestCommand(
    Guid RequestId,
    string Message,
    Guid ActorAccountId) : ICommand<(CarpoolResponseOutcome Outcome, int SeatsLeft)>;

internal sealed class AcceptCarpoolRequestCommandValidator
    : AbstractValidator<AcceptCarpoolRequestCommand>
{
    public AcceptCarpoolRequestCommandValidator()
    {
        RuleFor(c => c.RequestId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();

        RuleFor(c => c.Message)
            .MaximumLength(CarpoolRequestDraftValidator.MaxMessageLength)
            .WithMessage("Meddelandet är för långt.");
    }
}

internal sealed class DenyCarpoolRequestCommandValidator
    : AbstractValidator<DenyCarpoolRequestCommand>
{
    public DenyCarpoolRequestCommandValidator()
    {
        RuleFor(c => c.RequestId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();

        /*
         * Sakerhetschecklistan 2.10. Ett tyst nej far inte forekomma -- det ar en granne man
         * moter pa planen nasta lordag. Granssnittet erbjuder fardiga formuleringar, men
         * kravet ar serverside sa att inget annat anrop kan komma runt det.
         */
        RuleFor(c => c.Message)
            .NotEmpty().WithMessage("Ett nekande måste ha ett meddelande.")
            .MaximumLength(CarpoolRequestDraftValidator.MaxMessageLength)
            .WithMessage("Meddelandet är för långt.");
    }
}

internal sealed class AcceptCarpoolRequestCommandHandler(CarpoolResponseService service)
    : ICommandHandler<AcceptCarpoolRequestCommand, (CarpoolResponseOutcome, int)>
{
    public Task<(CarpoolResponseOutcome, int)> HandleAsync(
        AcceptCarpoolRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.AcceptAsync(
            command.RequestId, command.Message, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class DenyCarpoolRequestCommandHandler(CarpoolResponseService service)
    : ICommandHandler<DenyCarpoolRequestCommand, (CarpoolResponseOutcome, int)>
{
    public Task<(CarpoolResponseOutcome, int)> HandleAsync(
        DenyCarpoolRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DenyAsync(
            command.RequestId, command.Message, command.ActorAccountId, cancellationToken);
    }
}
