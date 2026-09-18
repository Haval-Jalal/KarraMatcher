using FluentValidation;

namespace KarraMatcher.Application.Features.Events.GetEvent;

/// <summary>
/// Ett tomt id är alltid ett anropsfel och ska aldrig nå databasen. Formatet i sig
/// kontrolleras redan av routingen, som avvisar allt som inte är en giltig Guid.
/// </summary>
internal sealed class GetEventQueryValidator : AbstractValidator<GetEventQuery>
{
    public GetEventQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty().WithMessage("Händelsen måste anges.");
    }
}
