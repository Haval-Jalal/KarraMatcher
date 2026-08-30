using FluentValidation;

namespace KarraMatcher.Application.Features.Matches.GetMatch;

/// <summary>
/// Ett tomt id är alltid ett anropsfel och ska aldrig nå databasen. Formatet i sig
/// kontrolleras redan av routingen, som avvisar allt som inte är en giltig Guid.
/// </summary>
internal sealed class GetMatchQueryValidator : AbstractValidator<GetMatchQuery>
{
    public GetMatchQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty().WithMessage("Matchen måste anges.");
    }
}
