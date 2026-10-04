using FluentValidation;

namespace KarraMatcher.Application.Features.Events.Admin;

/// <summary>
/// Tomma id:n och slugs är alltid anropsfel och ska avvisas med 400 innan de når databasen — utan
/// en validator hoppar <c>CommandValidationBehavior</c> tyst över kontrollen (`#612`). Sluggen
/// kommer ur URL:en och är användarindata; samma stränga format som läs-vägarna (`#586`).
/// </summary>
internal sealed class CancelEventCommandValidator : AbstractValidator<CancelEventCommand>
{
    public CancelEventCommandValidator()
    {
        RuleFor(command => command.TeamSlug).TeamSlug();
        RuleFor(command => command.EventId).NotEmpty().WithMessage("Händelsen måste anges.");
        RuleFor(command => command.ActorAccountId).NotEmpty().WithMessage("Kontot måste anges.");
    }
}

internal sealed class DeleteEventCommandValidator : AbstractValidator<DeleteEventCommand>
{
    public DeleteEventCommandValidator()
    {
        RuleFor(command => command.TeamSlug).TeamSlug();
        RuleFor(command => command.EventId).NotEmpty().WithMessage("Händelsen måste anges.");
        RuleFor(command => command.ActorAccountId).NotEmpty().WithMessage("Kontot måste anges.");
    }
}

internal sealed class CancelTruppEventCommandValidator : AbstractValidator<CancelTruppEventCommand>
{
    public CancelTruppEventCommandValidator()
    {
        RuleFor(command => command.TruppId).NotEmpty().WithMessage("Truppen måste anges.");
        RuleFor(command => command.EventId).NotEmpty().WithMessage("Händelsen måste anges.");
        RuleFor(command => command.ActorAccountId).NotEmpty().WithMessage("Kontot måste anges.");
    }
}

internal sealed class DeleteTruppEventCommandValidator : AbstractValidator<DeleteTruppEventCommand>
{
    public DeleteTruppEventCommandValidator()
    {
        RuleFor(command => command.TruppId).NotEmpty().WithMessage("Truppen måste anges.");
        RuleFor(command => command.EventId).NotEmpty().WithMessage("Händelsen måste anges.");
        RuleFor(command => command.ActorAccountId).NotEmpty().WithMessage("Kontot måste anges.");
    }
}

/// <summary>En lag-slug: små bokstäver, siffror och bindestreck. Delad regel, en text (`#586`).</summary>
internal static class EventCommandRules
{
    public static IRuleBuilderOptions<T, string> TeamSlug<T>(
        this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Laget måste anges.")
            .MaximumLength(80).WithMessage("Lagnamnet är för långt.")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Laget kan bara innehålla små bokstäver, siffror och bindestreck.");
}
