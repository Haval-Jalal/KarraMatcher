using FluentValidation;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Åtgärds-commands på en samåkning utan fritext (`#612`). Tomma id:n är alltid anropsfel och ska
/// avvisas med 400 innan de når databasen — utan en validator hoppar
/// <c>CommandValidationBehavior</c> tyst över kontrollen. Ägarskapet prövas ändå i tjänsten.
/// </summary>
internal sealed class WithdrawCarpoolOfferCommandValidator
    : AbstractValidator<WithdrawCarpoolOfferCommand>
{
    public WithdrawCarpoolOfferCommandValidator()
    {
        RuleFor(command => command.OfferId).NotEmpty().WithMessage("Erbjudandet måste anges.");
        RuleFor(command => command.ActorAccountId).NotEmpty().WithMessage("Kontot måste anges.");
    }
}

internal sealed class RetractCarpoolRequestCommandValidator
    : AbstractValidator<RetractCarpoolRequestCommand>
{
    public RetractCarpoolRequestCommandValidator()
    {
        RuleFor(command => command.RequestId).NotEmpty().WithMessage("Förfrågan måste anges.");
        RuleFor(command => command.ActorAccountId).NotEmpty().WithMessage("Kontot måste anges.");
    }
}
