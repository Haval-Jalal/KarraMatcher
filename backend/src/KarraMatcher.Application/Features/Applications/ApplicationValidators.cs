using FluentValidation;

namespace KarraMatcher.Application.Features.Applications;

internal sealed class SubmitApplicationCommandValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
    }
}

internal sealed class ApproveApplicationCommandValidator : AbstractValidator<ApproveApplicationCommand>
{
    public ApproveApplicationCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.Id).NotEmpty();
    }
}

internal sealed class DenyApplicationCommandValidator : AbstractValidator<DenyApplicationCommand>
{
    public DenyApplicationCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.Id).NotEmpty();
    }
}
