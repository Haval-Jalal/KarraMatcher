using FluentValidation;

namespace KarraMatcher.Application.Features.Auth.RequestLoginCode;

/// <summary>
/// Kontrollerar att adressen ser ut som en adress.
///
/// <para>
/// Formkontroll och ingenting annat. Att adressen finns hos oss får aldrig påverka
/// svaret — det är just den skillnaden som gör en inloggningsruta till en adresslista.
/// </para>
/// </summary>
internal sealed class RequestLoginCodeCommandValidator : AbstractValidator<RequestLoginCodeCommand>
{
    public RequestLoginCodeCommandValidator()
    {
        RuleFor(c => c.Email)
            .NotEmpty().WithMessage("Fyll i din mejladress.")
            .MaximumLength(320).WithMessage("Mejladressen är för lång.")
            .EmailAddress().WithMessage("Mejladressen ser inte riktig ut.");
    }
}
