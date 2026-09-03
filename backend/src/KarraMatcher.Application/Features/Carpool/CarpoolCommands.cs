using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Carpool;

/*
 * Tunna omslag runt CarpoolOfferService, av samma skal som matchernas kommandon: tjansten
 * haller reglerna, kommandona ger dem validering och en gemensam vag in genom dispatchern.
 */

/// <summary>Lägger upp ett erbjudande på matchen adressen pekar ut.</summary>
public sealed record CreateCarpoolOfferCommand(
    Guid MatchId,
    CarpoolOfferDraft Draft,
    Guid DriverAccountId) : ICommand<CarpoolOfferDto?>;

/// <summary>Drar tillbaka ett erbjudande. Bara ägaren kan.</summary>
public sealed record WithdrawCarpoolOfferCommand(Guid OfferId, Guid ActorAccountId)
    : ICommand<bool>;

/// <summary>Matchens öppna erbjudanden. <c>Reader</c> är null för en gäst.</summary>
public sealed record ListCarpoolOffersQuery(Guid MatchId, Guid? Reader)
    : IQuery<IReadOnlyList<CarpoolOfferDto>>;

internal sealed class CreateCarpoolOfferCommandHandler(CarpoolOfferService service)
    : ICommandHandler<CreateCarpoolOfferCommand, CarpoolOfferDto?>
{
    public Task<CarpoolOfferDto?> HandleAsync(
        CreateCarpoolOfferCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.CreateAsync(
            command.MatchId, command.Draft, command.DriverAccountId, cancellationToken);
    }
}

internal sealed class WithdrawCarpoolOfferCommandHandler(CarpoolOfferService service)
    : ICommandHandler<WithdrawCarpoolOfferCommand, bool>
{
    public Task<bool> HandleAsync(
        WithdrawCarpoolOfferCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.WithdrawAsync(command.OfferId, command.ActorAccountId, cancellationToken);
    }
}

internal sealed class ListCarpoolOffersQueryHandler(CarpoolOfferService service)
    : IQueryHandler<ListCarpoolOffersQuery, IReadOnlyList<CarpoolOfferDto>>
{
    public Task<IReadOnlyList<CarpoolOfferDto>> HandleAsync(
        ListCarpoolOffersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.ListAsync(query.MatchId, query.Reader, cancellationToken);
    }
}
