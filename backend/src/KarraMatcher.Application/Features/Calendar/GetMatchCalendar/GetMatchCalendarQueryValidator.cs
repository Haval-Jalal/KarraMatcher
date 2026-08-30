using FluentValidation;

namespace KarraMatcher.Application.Features.Calendar.GetMatchCalendar;

/// <summary>Ett tomt id är alltid ett anropsfel och ska aldrig nå databasen.</summary>
internal sealed class GetMatchCalendarQueryValidator : AbstractValidator<GetMatchCalendarQuery>
{
    public GetMatchCalendarQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty().WithMessage("Matchen måste anges.");
    }
}
