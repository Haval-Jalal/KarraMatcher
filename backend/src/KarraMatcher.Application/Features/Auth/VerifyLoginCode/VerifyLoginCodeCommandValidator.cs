using FluentValidation;

namespace KarraMatcher.Application.Features.Auth.VerifyLoginCode;

internal sealed class VerifyLoginCodeCommandValidator : AbstractValidator<VerifyLoginCodeCommand>
{
    public VerifyLoginCodeCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Fyll i din mejladress.")
            .MaximumLength(320);

        // Bara formen. En kod som inte stämmer avvisas av verifieringen, inte här --
        // annars hade ett valideringsfel skilt sig från ett felaktigt försök, och den
        // skillnaden går att mäta.
        RuleFor(c => c.Code)
            .NotEmpty().WithMessage("Fyll i koden från mejlet.")
            .Length(6).WithMessage("Koden är sex siffror.")
            .Matches("^[0-9]{6}$").WithMessage("Koden är sex siffror.");
    }
}
