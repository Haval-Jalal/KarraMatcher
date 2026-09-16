using FluentValidation;

namespace KarraMatcher.Application.Features.Invitations;

internal sealed class CreateInvitationCommandValidator : AbstractValidator<CreateInvitationCommand>
{
    public CreateInvitationCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Fyll i adressen till den du vill bjuda in.")
            .EmailAddress().WithMessage("Adressen ser inte giltig ut.")
            .MaximumLength(320);
    }
}

internal sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
    }
}

internal sealed class RevokeInvitationCommandValidator : AbstractValidator<RevokeInvitationCommand>
{
    public RevokeInvitationCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.Id).NotEmpty();
    }
}
