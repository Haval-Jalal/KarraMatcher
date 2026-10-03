using FluentValidation;

namespace KarraMatcher.Application.Features.Passkeys;

/// <summary>
/// Både kontot och nyckeln måste anges — ett tomt id är alltid ett anropsfel och ska avvisas med
/// 400 innan borttagningen slår mot databasen (`#612`).
/// </summary>
internal sealed class RemovePasskeyCommandValidator : AbstractValidator<RemovePasskeyCommand>
{
    public RemovePasskeyCommandValidator()
    {
        RuleFor(command => command.AccountId).NotEmpty().WithMessage("Kontot måste anges.");
        RuleFor(command => command.PasskeyId).NotEmpty().WithMessage("Nyckeln måste anges.");
    }
}
