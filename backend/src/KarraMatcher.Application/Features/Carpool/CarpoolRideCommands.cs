using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Carpool;

// ---- Skjutsförfrågan (passagerarens sida) --------------------------------------------

/// <summary>En förälder ber om skjuts. <c>MatchId</c> kommer ur route:n (grindad av MemberOfEvent).</summary>
public sealed record CreateCarpoolRideRequestCommand(
    Guid MatchId,
    CarpoolRideRequestDraft Draft,
    Guid RequesterAccountId)
    : ICommand<(CarpoolRideRequestOutcome Outcome, CarpoolRideRequestDto? Request)>;

/// <summary>Drar tillbaka en skjutsförfrågan. Bara den som frågade kan.</summary>
public sealed record WithdrawCarpoolRideRequestCommand(Guid RequestId, Guid ActorAccountId)
    : ICommand<bool>;

/// <summary>Matchens öppna skjutsförfrågningar, sedda av <c>Reader</c>.</summary>
public sealed record ListCarpoolRideRequestsQuery(Guid MatchId, Guid Reader)
    : IQuery<IReadOnlyList<CarpoolRideRequestDto>>;

// ---- Platserbjudande (förarens sida) -------------------------------------------------

/// <summary>
/// En förare erbjuder plats. <c>MatchId</c> (route) måste stämma med förfrågans match (§KM.3, jfr
/// `#470`): under-routen grindas av MemberOfEvent på matchen, men kommandot agerar på förfrågans id.
/// </summary>
public sealed record OfferCarpoolSeatCommand(
    Guid MatchId,
    Guid RideRequestId,
    CarpoolRideOfferDraft Draft,
    Guid DriverAccountId)
    : ICommand<(CarpoolRideOfferOutcome Outcome, CarpoolRideOfferDto? Offer)>;

/// <summary>En skjutsförfrågans platserbjudanden, sedda av <c>Reader</c>.</summary>
public sealed record ListCarpoolRideOffersQuery(Guid RideRequestId, Guid Reader)
    : IQuery<IReadOnlyList<CarpoolRideOfferDto>>;

/// <summary>Den som frågade tackar ja. Meddelandet är valfritt — ett ja behöver inga ord.</summary>
public sealed record AcceptCarpoolRideOfferCommand(Guid OfferId, string? Message, Guid ActorAccountId)
    : ICommand<CarpoolRideAnswerOutcome>;

/// <summary>Den som frågade tackar nej. Meddelandet är obligatoriskt (§KM.12) — redan i typen.</summary>
public sealed record DenyCarpoolRideOfferCommand(Guid OfferId, string Message, Guid ActorAccountId)
    : ICommand<CarpoolRideAnswerOutcome>;

/// <summary>Föraren återtar sitt platserbjudande.</summary>
public sealed record RetractCarpoolRideOfferCommand(Guid OfferId, Guid ActorAccountId)
    : ICommand<bool>;

// ---- Validatorer ---------------------------------------------------------------------

internal sealed class CarpoolRideRequestDraftValidator : AbstractValidator<CarpoolRideRequestDraft>
{
    public CarpoolRideRequestDraftValidator()
    {
        RuleFor(d => d.Direction).IsInEnum();

        RuleFor(d => d.Seats)
            .InclusiveBetween(1, CarpoolOfferDraftValidator.MaxSeats)
            .WithMessage(
                $"Antalet platser måste vara mellan 1 och {CarpoolOfferDraftValidator.MaxSeats}.");

        RuleFor(d => d.Note)
            .MaximumLength(CarpoolRequestDraftValidator.MaxMessageLength)
            .WithMessage("Notisen är för lång.");
    }
}

internal sealed class CarpoolRideOfferDraftValidator : AbstractValidator<CarpoolRideOfferDraft>
{
    public CarpoolRideOfferDraftValidator()
    {
        RuleFor(d => d.Seats)
            .InclusiveBetween(1, CarpoolOfferDraftValidator.MaxSeats)
            .WithMessage(
                $"Antalet platser måste vara mellan 1 och {CarpoolOfferDraftValidator.MaxSeats}.");

        RuleFor(d => d.Message)
            .MaximumLength(CarpoolRequestDraftValidator.MaxMessageLength)
            .WithMessage("Hälsningen är för lång.");
    }
}

internal sealed class CreateCarpoolRideRequestCommandValidator
    : AbstractValidator<CreateCarpoolRideRequestCommand>
{
    public CreateCarpoolRideRequestCommandValidator()
    {
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.RequesterAccountId).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new CarpoolRideRequestDraftValidator()!);
    }
}

internal sealed class OfferCarpoolSeatCommandValidator : AbstractValidator<OfferCarpoolSeatCommand>
{
    public OfferCarpoolSeatCommandValidator()
    {
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.RideRequestId).NotEmpty();
        RuleFor(c => c.DriverAccountId).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new CarpoolRideOfferDraftValidator()!);
    }
}

internal sealed class AcceptCarpoolRideOfferCommandValidator
    : AbstractValidator<AcceptCarpoolRideOfferCommand>
{
    public AcceptCarpoolRideOfferCommandValidator()
    {
        RuleFor(c => c.OfferId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();
        RuleFor(c => c.Message)
            .MaximumLength(CarpoolRequestDraftValidator.MaxMessageLength)
            .WithMessage("Meddelandet är för långt.");
    }
}

internal sealed class DenyCarpoolRideOfferCommandValidator
    : AbstractValidator<DenyCarpoolRideOfferCommand>
{
    public DenyCarpoolRideOfferCommandValidator()
    {
        RuleFor(c => c.OfferId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();

        // Säkerhetschecklistan 2.10 / §KM.12: ett tyst nej får inte förekomma. Serverside så inget
        // annat anrop kan komma runt det.
        RuleFor(c => c.Message)
            .NotEmpty().WithMessage("Ett nekande måste ha ett meddelande.")
            .MaximumLength(CarpoolRequestDraftValidator.MaxMessageLength)
            .WithMessage("Meddelandet är för långt.");
    }
}

// ---- Hanterare -----------------------------------------------------------------------

internal sealed class CreateCarpoolRideRequestCommandHandler(CarpoolRideService service)
    : ICommandHandler<CreateCarpoolRideRequestCommand,
        (CarpoolRideRequestOutcome, CarpoolRideRequestDto?)>
{
    public Task<(CarpoolRideRequestOutcome, CarpoolRideRequestDto?)> HandleAsync(
        CreateCarpoolRideRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateRequestAsync(
            command.MatchId, command.Draft, command.RequesterAccountId, cancellationToken);
    }
}

internal sealed class WithdrawCarpoolRideRequestCommandHandler(CarpoolRideService service)
    : ICommandHandler<WithdrawCarpoolRideRequestCommand, bool>
{
    public Task<bool> HandleAsync(
        WithdrawCarpoolRideRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.WithdrawRequestAsync(
            command.RequestId, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class ListCarpoolRideRequestsQueryHandler(CarpoolRideService service)
    : IQueryHandler<ListCarpoolRideRequestsQuery, IReadOnlyList<CarpoolRideRequestDto>>
{
    public Task<IReadOnlyList<CarpoolRideRequestDto>> HandleAsync(
        ListCarpoolRideRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListRequestsAsync(query.MatchId, query.Reader, cancellationToken);
    }
}

internal sealed class OfferCarpoolSeatCommandHandler(CarpoolRideService service)
    : ICommandHandler<OfferCarpoolSeatCommand, (CarpoolRideOfferOutcome, CarpoolRideOfferDto?)>
{
    public Task<(CarpoolRideOfferOutcome, CarpoolRideOfferDto?)> HandleAsync(
        OfferCarpoolSeatCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.OfferSeatAsync(
            command.MatchId,
            command.RideRequestId,
            command.Draft,
            command.DriverAccountId,
            cancellationToken);
    }
}

internal sealed class ListCarpoolRideOffersQueryHandler(CarpoolRideService service)
    : IQueryHandler<ListCarpoolRideOffersQuery, IReadOnlyList<CarpoolRideOfferDto>>
{
    public Task<IReadOnlyList<CarpoolRideOfferDto>> HandleAsync(
        ListCarpoolRideOffersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListOffersAsync(query.RideRequestId, query.Reader, cancellationToken);
    }
}

internal sealed class AcceptCarpoolRideOfferCommandHandler(CarpoolRideService service)
    : ICommandHandler<AcceptCarpoolRideOfferCommand, CarpoolRideAnswerOutcome>
{
    public Task<CarpoolRideAnswerOutcome> HandleAsync(
        AcceptCarpoolRideOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.AcceptOfferAsync(
            command.OfferId, command.Message, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class DenyCarpoolRideOfferCommandHandler(CarpoolRideService service)
    : ICommandHandler<DenyCarpoolRideOfferCommand, CarpoolRideAnswerOutcome>
{
    public Task<CarpoolRideAnswerOutcome> HandleAsync(
        DenyCarpoolRideOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.DenyOfferAsync(
            command.OfferId, command.Message, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class RetractCarpoolRideOfferCommandHandler(CarpoolRideService service)
    : ICommandHandler<RetractCarpoolRideOfferCommand, bool>
{
    public Task<bool> HandleAsync(
        RetractCarpoolRideOfferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RetractOfferAsync(command.OfferId, command.ActorAccountId, cancellationToken);
    }
}
