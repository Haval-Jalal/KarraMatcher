using FluentValidation;

namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Delade regler för superadmins indata (§KM.3, `#192`).
///
/// <para>
/// Slugen är avsiktligt strikt: den lever i delade länkar och som filnamnssäker
/// identifierare, och inga svenska tecken får förekomma (§KM.9). Färgen måste vara en
/// hex-kod eftersom den driver appens tema direkt i CSS.
/// </para>
/// </summary>
internal static class AdminValidation
{
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";
    public const string ColorPattern = "^#[0-9a-fA-F]{6}$";

    public const string SlugMessage =
        "Slugen får bara innehålla små bokstäver a–z, siffror och bindestreck.";
    public const string ColorMessage = "Färgen måste vara en hex-kod, t.ex. #D9A21B.";
}

internal sealed class CreateSportCommandValidator : AbstractValidator<CreateSportCommand>
{
    public CreateSportCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i sportens namn.").MaximumLength(50);
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(50)
            .Matches(AdminValidation.SlugPattern).WithMessage(AdminValidation.SlugMessage);
    }
}

internal sealed class UpdateSportCommandValidator : AbstractValidator<UpdateSportCommand>
{
    public UpdateSportCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i sportens namn.").MaximumLength(50);
    }
}

internal sealed class CreateClubCommandValidator : AbstractValidator<CreateClubCommand>
{
    public CreateClubCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i klubbens namn.").MaximumLength(100);
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(50)
            .Matches(AdminValidation.SlugPattern).WithMessage(AdminValidation.SlugMessage);
    }
}

internal sealed class UpdateClubCommandValidator : AbstractValidator<UpdateClubCommand>
{
    public UpdateClubCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i klubbens namn.").MaximumLength(100);
    }
}

internal sealed class CreateTruppCommandValidator : AbstractValidator<CreateTruppCommand>
{
    public CreateTruppCommandValidator()
    {
        RuleFor(c => c.ClubId).NotEmpty().WithMessage("Välj en klubb.");
        RuleFor(c => c.SportId).NotEmpty().WithMessage("Välj en sport.");
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i truppens namn.").MaximumLength(50);
        RuleFor(c => c.Season).NotEmpty().WithMessage("Fyll i säsongen.").MaximumLength(20);
    }
}

internal sealed class UpdateTruppCommandValidator : AbstractValidator<UpdateTruppCommand>
{
    public UpdateTruppCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.SportId).NotEmpty().WithMessage("Välj en sport.");
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i truppens namn.").MaximumLength(50);
        RuleFor(c => c.Season).NotEmpty().WithMessage("Fyll i säsongen.").MaximumLength(20);
    }
}

internal sealed class CreateLagCommandValidator : AbstractValidator<CreateLagCommand>
{
    public CreateLagCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty().WithMessage("Välj en trupp.");
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i lagets namn.").MaximumLength(50);
        RuleFor(c => c.ColorHex).NotEmpty().MaximumLength(7)
            .Matches(AdminValidation.ColorPattern).WithMessage(AdminValidation.ColorMessage);
        RuleFor(c => c.Slug).NotEmpty().MaximumLength(50)
            .Matches(AdminValidation.SlugPattern).WithMessage(AdminValidation.SlugMessage);
    }
}

internal sealed class UpdateLagCommandValidator : AbstractValidator<UpdateLagCommand>
{
    public UpdateLagCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().WithMessage("Fyll i lagets namn.").MaximumLength(50);
        RuleFor(c => c.ColorHex).NotEmpty().MaximumLength(7)
            .Matches(AdminValidation.ColorPattern).WithMessage(AdminValidation.ColorMessage);
    }
}

internal sealed class GrantAdminCommandValidator : AbstractValidator<GrantAdminCommand>
{
    public GrantAdminCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Fyll i adressen till den som ska bli admin.")
            .EmailAddress().WithMessage("Adressen ser inte giltig ut.")
            .MaximumLength(200);
    }
}

internal sealed class RevokeAdminCommandValidator : AbstractValidator<RevokeAdminCommand>
{
    public RevokeAdminCommandValidator()
    {
        RuleFor(c => c.TruppId).NotEmpty();
        RuleFor(c => c.AccountId).NotEmpty();
    }
}
