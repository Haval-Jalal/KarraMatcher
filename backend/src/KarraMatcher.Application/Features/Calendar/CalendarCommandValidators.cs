using FluentValidation;

namespace KarraMatcher.Application.Features.Calendar;

/// <summary>
/// Kalender-feeden nås anonymt med nyckeln i sökvägen (§KM.4). En tom eller orimligt lång nyckel
/// är alltid ett anropsfel och ska avvisas innan den slår mot databasen — utan validator skulle
/// den gå rakt in i en hash-slagning (`#612`). Nyckeln är 32 byte, så 128 tecken rymmer varje
/// kodning med marginal.
/// </summary>
internal sealed class GetCalendarFeedQueryValidator : AbstractValidator<GetCalendarFeedQuery>
{
    public GetCalendarFeedQueryValidator()
    {
        RuleFor(query => query.Token)
            .NotEmpty().WithMessage("Kalender-nyckeln måste anges.")
            .MaximumLength(128).WithMessage("Kalender-nyckeln är för lång.");
    }
}

/// <summary>Kontot kommer från den inloggade — ett tomt id är alltid ett anropsfel (`#612`).</summary>
internal sealed class RegenerateCalendarTokenCommandValidator
    : AbstractValidator<RegenerateCalendarTokenCommand>
{
    public RegenerateCalendarTokenCommandValidator()
    {
        RuleFor(command => command.AccountId)
            .NotEmpty().WithMessage("Kontot måste anges.");
    }
}
