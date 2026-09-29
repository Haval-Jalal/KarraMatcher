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

/*
 * Kommandona nedan har inga fritextfält — bara id:n — men saknade tidigare validator helt, så
 * CommandValidationBehavior hoppade tyst över dem (`#403`). NotEmpty-reglerna avvisar ett tomt
 * Guid tidigt med ett begripligt fel i stället för att låta ett meningslöst id nå tjänsten.
 */

/// <summary>Ett cup-lag att ta bort måste pekas ut.</summary>
internal sealed class DeleteCupTeamCommandValidator : AbstractValidator<DeleteCupTeamCommand>
{
    public DeleteCupTeamCommandValidator()
    {
        RuleFor(command => command.TruppId).NotEmpty();
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.CupTeamId).NotEmpty();
    }
}

/// <summary>Både laget och barnet måste pekas ut vid placering.</summary>
internal sealed class AssignCupChildCommandValidator : AbstractValidator<AssignCupChildCommand>
{
    public AssignCupChildCommandValidator()
    {
        RuleFor(command => command.TruppId).NotEmpty();
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.CupTeamId).NotEmpty();
        RuleFor(command => command.ChildId).NotEmpty();
    }
}

/// <summary>Både laget och barnet måste pekas ut när barnet tas bort ur laget.</summary>
internal sealed class UnassignCupChildCommandValidator : AbstractValidator<UnassignCupChildCommand>
{
    public UnassignCupChildCommandValidator()
    {
        RuleFor(command => command.TruppId).NotEmpty();
        RuleFor(command => command.EventId).NotEmpty();
        RuleFor(command => command.CupTeamId).NotEmpty();
        RuleFor(command => command.ChildId).NotEmpty();
    }
}
