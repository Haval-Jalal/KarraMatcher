using FluentValidation;

namespace KarraMatcher.Application.Features.Teams.GetTeamMatches;

/// <summary>
/// Sluggen kommer från URL:en och är därför användarindata.
///
/// <para>
/// Kontrollen är avsiktligt sträng: en slug består av små bokstäver, siffror och
/// bindestreck. Allt annat avvisas med 400 innan det når databasen, i stället för att
/// resultera i en meningslös sökning på skräp.
/// </para>
/// </summary>
internal sealed class GetTeamMatchesQueryValidator : AbstractValidator<GetTeamMatchesQuery>
{
    public GetTeamMatchesQueryValidator()
    {
        RuleFor(query => query.Slug)
            .NotEmpty().WithMessage("Laget måste anges.")
            .MaximumLength(80).WithMessage("Lagnamnet är för långt.")
            .Matches("^[a-z0-9-]+$")
            .WithMessage("Laget kan bara innehålla små bokstäver, siffror och bindestreck.");
    }
}
