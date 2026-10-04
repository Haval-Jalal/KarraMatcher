using FluentValidation;

namespace KarraMatcher.Application.Features.Auth.DeleteAccount;

/// <summary>
/// Kontot kommer från den inloggade — ett tomt id är alltid ett anropsfel och ska avvisas innan en
/// radering drar igång mot fel (eller inget) konto (`#612`).
/// </summary>
internal sealed class DeleteAccountCommandValidator : AbstractValidator<DeleteAccountCommand>
{
    public DeleteAccountCommandValidator()
    {
        RuleFor(command => command.AccountId)
            .NotEmpty().WithMessage("Kontot måste anges.");
    }
}
