using FluentValidation;

using KarraMatcher.Domain.Cup;

namespace KarraMatcher.Application.Features.Cup;

/// <summary>Ett cup-lag måste ha ett namn, och det får inte vara orimligt långt (§KM.1: ingen barn-PII).</summary>
internal sealed class CreateCupTeamCommandValidator : AbstractValidator<CreateCupTeamCommand>
{
    public CreateCupTeamCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Ge cup-laget ett namn.")
            .MaximumLength(CupTeam.MaxName).WithMessage("Namnet är för långt.");
    }
}

/// <summary>Samma namnregel vid namnbyte som vid skapande.</summary>
internal sealed class RenameCupTeamCommandValidator : AbstractValidator<RenameCupTeamCommand>
{
    public RenameCupTeamCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Ge cup-laget ett namn.")
            .MaximumLength(CupTeam.MaxName).WithMessage("Namnet är för långt.");
    }
}
