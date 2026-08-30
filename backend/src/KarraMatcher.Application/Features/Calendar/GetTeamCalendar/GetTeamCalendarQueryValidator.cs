using FluentValidation;

namespace KarraMatcher.Application.Features.Calendar.GetTeamCalendar;

/// <summary>
/// Samma krav på sluggen som schemaendpointen — den kommer från URL:en och är därmed
/// användarindata.
/// </summary>
internal sealed class GetTeamCalendarQueryValidator : AbstractValidator<GetTeamCalendarQuery>
{
    public GetTeamCalendarQueryValidator()
    {
        RuleFor(query => query.Slug)
            .NotEmpty().WithMessage("Laget måste anges.")
            .MaximumLength(80).WithMessage("Lagnamnet är för långt.")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Laget kan bara innehålla små bokstäver, siffror och bindestreck.");
    }
}
