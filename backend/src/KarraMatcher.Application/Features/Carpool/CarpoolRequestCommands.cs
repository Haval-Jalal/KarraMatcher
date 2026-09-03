using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>Skickar en förfrågan om att få åka med.</summary>
public sealed record CreateCarpoolRequestCommand(
    Guid OfferId,
    CarpoolRequestDraft Draft,
    Guid RequesterAccountId) : ICommand<(CarpoolRequestOutcome Outcome, CarpoolRequestDto? Request)>;

/// <summary>Återtar en förfrågan. Bara den som frågade kan.</summary>
public sealed record RetractCarpoolRequestCommand(Guid RequestId, Guid ActorAccountId)
    : ICommand<bool>;

/// <summary>Ett erbjudandes förfrågningar, sedda av <c>Reader</c>.</summary>
public sealed record ListCarpoolRequestsQuery(Guid OfferId, Guid Reader)
    : IQuery<IReadOnlyList<CarpoolRequestDto>>;

/// <summary>
/// Vad en förfrågan måste ha.
///
/// <para>
/// Platserna prövas mot bilens tak, <b>inte mot erbjudandets lediga platser</b>. Att fråga
/// om fler platser än som finns kvar ska gå — föraren svarar "någon annan hann före"
/// i stället för att den som frågar möts av ett formulärfel (§KM.12).
/// </para>
/// </summary>
internal sealed class CarpoolRequestDraftValidator : AbstractValidator<CarpoolRequestDraft>
{
    public CarpoolRequestDraftValidator()
    {
        RuleFor(d => d.Seats)
            .InclusiveBetween(1, CarpoolOfferDraftValidator.MaxSeats)
            .WithMessage(
                $"Antalet platser måste vara mellan 1 och {CarpoolOfferDraftValidator.MaxSeats}.");

        RuleFor(d => d.Message)
            .MaximumLength(500).WithMessage("Hälsningen är för lång.");
    }
}

internal sealed class CreateCarpoolRequestCommandValidator
    : AbstractValidator<CreateCarpoolRequestCommand>
{
    public CreateCarpoolRequestCommandValidator()
    {
        RuleFor(c => c.OfferId).NotEmpty();
        RuleFor(c => c.RequesterAccountId).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new CarpoolRequestDraftValidator()!);
    }
}

internal sealed class CreateCarpoolRequestCommandHandler(CarpoolRequestService service)
    : ICommandHandler<CreateCarpoolRequestCommand, (CarpoolRequestOutcome, CarpoolRequestDto?)>
{
    public Task<(CarpoolRequestOutcome, CarpoolRequestDto?)> HandleAsync(
        CreateCarpoolRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.OfferId, command.Draft, command.RequesterAccountId, cancellationToken);
    }
}

internal sealed class RetractCarpoolRequestCommandHandler(CarpoolRequestService service)
    : ICommandHandler<RetractCarpoolRequestCommand, bool>
{
    public Task<bool> HandleAsync(
        RetractCarpoolRequestCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.RetractAsync(command.RequestId, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class ListCarpoolRequestsQueryHandler(CarpoolRequestService service)
    : IQueryHandler<ListCarpoolRequestsQuery, IReadOnlyList<CarpoolRequestDto>>
{
    public Task<IReadOnlyList<CarpoolRequestDto>> HandleAsync(
        ListCarpoolRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListAsync(query.OfferId, query.Reader, cancellationToken);
    }
}
