using FluentValidation;

using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Events.Admin;

/// <summary>
/// Vad en händelse måste ha för att gå att lägga upp (`#198`).
///
/// <para>
/// Reglerna är avsiktligt få. Tränaren fyller i det här på en telefon, ofta med barn runt
/// benen, och varje krav som inte fyller en funktion är ett hinder. Det som finns här är
/// sådant som annars ger ett obegripligt fel längre fram — eller en händelse som ingen hittar.
/// </para>
///
/// <para>
/// En match kräver motståndare och hemma/borta; en träning eller övrig händelse en rubrik.
/// </para>
/// </summary>
internal sealed class EventDraftValidator : AbstractValidator<EventDraft>
{
    public EventDraftValidator()
    {
        RuleFor(d => d.Type).IsInEnum().WithMessage("Välj en giltig typ.");

        RuleFor(d => d.VenueId)
            .NotEmpty().WithMessage("Välj en spelplats.");

        /*
         * Starttiden lagras i UTC (§KM.5). Kravet pa att den faktiskt ar UTC ar inte
         * formalia: en lokal tid som sparas rakt av blir tva timmar fel pa sommaren, och
         * felet syns forst i foraldrarnas kalendrar.
         */
        RuleFor(d => d.KickoffUtc)
            .Must(kickoff => kickoff.Kind != DateTimeKind.Local)
            .WithMessage("Starttiden måste anges i UTC.");

        RuleFor(d => d.Note)
            .MaximumLength(500).WithMessage("Notisen är för lång.");

        RuleFor(d => d.AddressOverride)
            .MaximumLength(200).WithMessage("Adressen är för lång.");

        // En match: motståndare och hemma/borta krävs, rubriken ignoreras.
        When(d => d.Type == EventType.Match, () =>
        {
            RuleFor(d => d.Opponent)
                .NotEmpty().WithMessage("Fyll i motståndarlaget.")
                .MaximumLength(120).WithMessage("Motståndarlagets namn är för långt.");

            RuleFor(d => d.IsHome)
                .NotNull().WithMessage("Ange om matchen är hemma eller borta.");
        });

        // Träning eller övrigt: en rubrik krävs, motståndare/hemma-borta gäller inte.
        When(d => d.Type != EventType.Match, () =>
        {
            RuleFor(d => d.Title)
                .NotEmpty().WithMessage("Fyll i en rubrik.")
                .MaximumLength(120).WithMessage("Rubriken är för lång.");
        });
    }
}

internal sealed class CreateEventCommandValidator : AbstractValidator<CreateEventCommand>
{
    public CreateEventCommandValidator()
    {
        RuleFor(c => c.TeamSlug).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new EventDraftValidator()!);
    }
}

internal sealed class UpdateEventCommandValidator : AbstractValidator<UpdateEventCommand>
{
    public UpdateEventCommandValidator()
    {
        RuleFor(c => c.TeamSlug).NotEmpty();
        RuleFor(c => c.EventId).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new EventDraftValidator()!);
    }
}
