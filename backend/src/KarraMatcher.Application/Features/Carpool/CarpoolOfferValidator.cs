using FluentValidation;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Vad ett erbjudande måste ha.
///
/// <para>
/// Reglerna är få med flit. Det här fylls i på en telefon, ofta strax innan avfärd, och
/// varje krav som inte fyller en funktion är ett hinder.
/// </para>
/// </summary>
internal sealed class CarpoolOfferDraftValidator : AbstractValidator<CarpoolOfferDraft>
{
    /// <summary>Taket. Vad som får plats i en vanlig bil utöver föraren och det egna barnet.</summary>
    public const int MaxSeats = 4;

    public CarpoolOfferDraftValidator()
    {
        RuleFor(d => d.Direction)
            .IsInEnum().WithMessage("Välj om du kör till, från eller båda hållen.");

        RuleFor(d => d.DeparturePlace)
            .NotEmpty().WithMessage("Skriv var ni åker ifrån.")
            .MaximumLength(120).WithMessage("Avgångsplatsen är för lång.");

        /*
         * Avgangen lagras i UTC (§KM.5). Kravet ar inte formalia: en lokal tid som sparas
         * rakt av blir tva timmar fel pa sommaren, och da star nagon pa fel plats vid fel
         * klockslag.
         */
        RuleFor(d => d.DepartureUtc)
            .Must(departure => departure.Kind != DateTimeKind.Local)
            .WithMessage("Avgångstiden måste anges i UTC.");

        RuleFor(d => d.Seats)
            .InclusiveBetween(1, MaxSeats)
            .WithMessage($"Antalet platser måste vara mellan 1 och {MaxSeats}.");

        RuleFor(d => d.Note)
            .MaximumLength(500).WithMessage("Notisen är för lång.");
    }
}

internal sealed class CreateCarpoolOfferCommandValidator : AbstractValidator<CreateCarpoolOfferCommand>
{
    public CreateCarpoolOfferCommandValidator()
    {
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.DriverAccountId).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new CarpoolOfferDraftValidator()!);
    }
}
