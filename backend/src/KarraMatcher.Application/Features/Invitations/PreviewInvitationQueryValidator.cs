using FluentValidation;

namespace KarraMatcher.Application.Features.Invitations;

/// <summary>
/// Förhandsvisningen är anonym och tar inbjudnings-token ur sökvägen (§KM.3). En tom eller orimligt
/// lång token är alltid ett anropsfel och ska avvisas innan den slår mot en hash-slagning (`#612`).
/// </summary>
internal sealed class PreviewInvitationQueryValidator : AbstractValidator<PreviewInvitationQuery>
{
    public PreviewInvitationQueryValidator()
    {
        RuleFor(query => query.Token)
            .NotEmpty().WithMessage("Inbjudningslänken måste anges.")
            .MaximumLength(128).WithMessage("Inbjudningslänken är för lång.");
    }
}
