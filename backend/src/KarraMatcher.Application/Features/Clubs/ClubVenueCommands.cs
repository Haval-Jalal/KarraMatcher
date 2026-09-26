using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Clubs;

/// <summary>Klubbens hemmaplan för en trupp (`#307`).</summary>
public sealed record GetClubVenueQuery(Guid TruppId) : IQuery<ClubVenueDto?>;

/// <summary>Sätter (eller ändrar) klubbens hemmaplan via en av dess truppar (`#307`).</summary>
public sealed record SetClubVenueCommand(Guid TruppId, string Name, string Address, Guid ActorAccountId)
    : ICommand<SetClubVenueResult>;

internal sealed class SetClubVenueCommandValidator : AbstractValidator<SetClubVenueCommand>
{
    public SetClubVenueCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.ActorAccountId).NotEmpty();
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Ge planen ett namn.")
            .MaximumLength(100).WithMessage("Namnet är för långt.");
        RuleFor(c => c.Address)
            .NotEmpty().WithMessage("Skriv planens adress.")
            .MaximumLength(200).WithMessage("Adressen är för lång.");
    }
}

internal sealed class GetClubVenueQueryHandler(ClubVenueService service)
    : IQueryHandler<GetClubVenueQuery, ClubVenueDto?>
{
    public Task<ClubVenueDto?> HandleAsync(GetClubVenueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.GetByTruppAsync(query.TruppId, cancellationToken);
    }
}

internal sealed class SetClubVenueCommandHandler(ClubVenueService service)
    : ICommandHandler<SetClubVenueCommand, SetClubVenueResult>
{
    public Task<SetClubVenueResult> HandleAsync(
        SetClubVenueCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.SetByTruppAsync(
            command.TruppId, command.Name, command.Address, command.ActorAccountId, cancellationToken);
    }
}
