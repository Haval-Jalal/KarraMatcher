using FluentValidation;

namespace KarraMatcher.Application.Features.Matches.Admin;

/// <summary>
/// Vad en match måste ha för att gå att lägga upp.
///
/// <para>
/// Reglerna är avsiktligt få. Tränaren fyller i det här på en telefon, ofta med barn runt
/// benen, och varje krav som inte fyller en funktion är ett hinder. Det som finns här är
/// sådant som annars ger ett obegripligt fel längre fram — eller en match som ingen hittar.
/// </para>
/// </summary>
internal sealed class MatchDraftValidator : AbstractValidator<MatchDraft>
{
    public MatchDraftValidator()
    {
        RuleFor(d => d.Opponent)
            .NotEmpty().WithMessage("Fyll i motståndarlaget.")
            .MaximumLength(120).WithMessage("Motståndarlagets namn är för långt.");

        RuleFor(d => d.VenueId)
            .NotEmpty().WithMessage("Välj en spelplats.");

        /*
         * Avsparken lagras i UTC (§KM.5). Kravet pa att den faktiskt ar UTC ar inte
         * formalia: en lokal tid som sparas rakt av blir tva timmar fel pa sommaren, och
         * felet syns forst i foraldrarnas kalendrar.
         */
        RuleFor(d => d.KickoffUtc)
            .Must(kickoff => kickoff.Kind != DateTimeKind.Local)
            .WithMessage("Avsparkstiden måste anges i UTC.");

        RuleFor(d => d.Note)
            .MaximumLength(500).WithMessage("Notisen är för lång.");

        RuleFor(d => d.AddressOverride)
            .MaximumLength(200).WithMessage("Adressen är för lång.");
    }
}

internal sealed class CreateMatchCommandValidator : AbstractValidator<CreateMatchCommand>
{
    public CreateMatchCommandValidator()
    {
        RuleFor(c => c.TeamSlug).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new MatchDraftValidator()!);
    }
}

internal sealed class UpdateMatchCommandValidator : AbstractValidator<UpdateMatchCommand>
{
    public UpdateMatchCommandValidator()
    {
        RuleFor(c => c.TeamSlug).NotEmpty();
        RuleFor(c => c.MatchId).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new MatchDraftValidator()!);
    }
}
