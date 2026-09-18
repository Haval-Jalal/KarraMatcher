using FluentValidation;

namespace KarraMatcher.Application.Features.Children;

/// <summary>
/// Reglerna för ett barns uppgifter (§KM.1). Efternamnet begränsas till en initial (max två
/// tecken, för t.ex. "Ø") — hela efternamnet ska aldrig gå att skriva in.
/// </summary>
internal sealed class CreateChildCommandValidator : AbstractValidator<CreateChildCommand>
{
    public CreateChildCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.FirstName).NotEmpty().WithMessage("Fyll i barnets förnamn.").MaximumLength(50);
        RuleFor(c => c.LastInitial)
            .NotEmpty().WithMessage("Fyll i efternamnets initial.")
            .MaximumLength(2).WithMessage("Bara efternamnets initial, inte hela efternamnet.");
    }
}

internal sealed class UpdateChildCommandValidator : AbstractValidator<UpdateChildCommand>
{
    public UpdateChildCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.FirstName).NotEmpty().WithMessage("Fyll i barnets förnamn.").MaximumLength(50);
        RuleFor(c => c.LastInitial)
            .NotEmpty().WithMessage("Fyll i efternamnets initial.")
            .MaximumLength(2).WithMessage("Bara efternamnets initial, inte hela efternamnet.");
    }
}

internal sealed class DeleteChildCommandValidator : AbstractValidator<DeleteChildCommand>
{
    public DeleteChildCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.Id).NotEmpty();
    }
}

internal sealed class LinkGuardianCommandValidator : AbstractValidator<LinkGuardianCommand>
{
    public LinkGuardianCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.ChildId).NotEmpty();
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Fyll i vårdnadshavarens adress.")
            .EmailAddress().WithMessage("Adressen ser inte giltig ut.")
            .MaximumLength(320);
    }
}

internal sealed class UnlinkGuardianCommandValidator : AbstractValidator<UnlinkGuardianCommand>
{
    public UnlinkGuardianCommandValidator()
    {
        RuleFor(c => c.AgeGroupId).NotEmpty();
        RuleFor(c => c.ChildId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
    }
}
