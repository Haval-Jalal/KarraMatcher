using FluentValidation;

namespace KarraMatcher.Application.Features.Consent;

internal sealed class GrantConsentCommandValidator : AbstractValidator<GrantConsentCommand>
{
    public GrantConsentCommandValidator()
    {
        RuleFor(c => c.AccountId).NotEmpty();
        RuleFor(c => c.Version).NotEmpty().WithMessage("Samtycket saknar version.");
    }
}
