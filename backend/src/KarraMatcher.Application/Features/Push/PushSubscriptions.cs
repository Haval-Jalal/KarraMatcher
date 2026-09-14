using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Att prenumerera på ett lags notiser, och att sluta (`#60`).
///
/// <h3>Utan konto</h3>
///
/// <para>
/// Prenumerationen hör till webbläsaren, inte till en inloggning (§KM.3, M7). Kräver
/// notiser ett konto når de en bråkdel av föräldrarna — och att lördagens match är
/// inställd är precis det slags upplysning som ska nå alla.
/// </para>
///
/// <h3>Adressen loggas aldrig</h3>
///
/// <para>
/// Push-adressen identifierar en enskild enhet lika bra som ett telefonnummer (§KM.10).
/// Den skrivs därför inte i loggar, inte i audit-rader, och lämnas aldrig ut i ett svar —
/// inte ens till den som just skickade in den. Svaret är tomt med flit.
/// </para>
/// </summary>
/// <param name="AccountId">
/// Kontot bakom webbläsaren, om någon är inloggad — annars null. Bara till för att kunna
/// rikta en samåkningsnotis (`#63`); en gäst prenumererar precis som förr.
/// </param>
public sealed record SubscribeToPushCommand(string Slug, PushSubscriptionDraft Draft, Guid? AccountId)
    : ICommand<bool>;

/// <summary>Slutar prenumerera. Adressen är den enda nyckel webbläsaren har.</summary>
public sealed record UnsubscribeFromPushCommand(string Slug, string Endpoint) : ICommand<bool>;

/// <summary>Det webbläsaren lämnar ifrån sig när användaren tillåter notiser.</summary>
public sealed record PushSubscriptionDraft(string Endpoint, string P256dh, string Auth);

internal sealed class PushSubscriptionDraftValidator : AbstractValidator<PushSubscriptionDraft>
{
    /// <summary>Samma tak som kolumnen.</summary>
    internal const int MaxEndpointLength = 2048;

    public PushSubscriptionDraftValidator()
    {
        /*
         * Adressen maste vara en absolut https-adress. Kontrollen ar inte formalia: varden
         * kommer fran en klient, och en relativ eller http-adress ar antingen ett trasigt
         * anrop eller nagon som provar vad servern accepterar.
         */
        RuleFor(d => d.Endpoint)
            .NotEmpty().WithMessage("Prenumerationen saknar adress.")
            .MaximumLength(MaxEndpointLength).WithMessage("Adressen är för lång.")
            .Must(BeHttpsUrl).WithMessage("Adressen måste vara en https-adress.");

        RuleFor(d => d.P256dh)
            .NotEmpty().WithMessage("Prenumerationen saknar nyckel.")
            .MaximumLength(256).WithMessage("Nyckeln är för lång.");

        RuleFor(d => d.Auth)
            .NotEmpty().WithMessage("Prenumerationen saknar hemlighet.")
            .MaximumLength(128).WithMessage("Hemligheten är för lång.");
    }

    private static bool BeHttpsUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;
}

internal sealed class SubscribeToPushCommandValidator : AbstractValidator<SubscribeToPushCommand>
{
    public SubscribeToPushCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty();
        RuleFor(c => c.Draft).NotNull().SetValidator(new PushSubscriptionDraftValidator()!);
    }
}

internal sealed class UnsubscribeFromPushCommandValidator
    : AbstractValidator<UnsubscribeFromPushCommand>
{
    public UnsubscribeFromPushCommandValidator()
    {
        RuleFor(c => c.Slug).NotEmpty();
        RuleFor(c => c.Endpoint)
            .NotEmpty()
            .MaximumLength(PushSubscriptionDraftValidator.MaxEndpointLength);
    }
}

internal sealed class SubscribeToPushCommandHandler(IPushSubscriptionRepository subscriptions)
    : ICommandHandler<SubscribeToPushCommand, bool>
{
    public Task<bool> HandleAsync(SubscribeToPushCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return subscriptions.SubscribeAsync(
            command.Slug, command.Draft, command.AccountId, cancellationToken);
    }
}

internal sealed class UnsubscribeFromPushCommandHandler(IPushSubscriptionRepository subscriptions)
    : ICommandHandler<UnsubscribeFromPushCommand, bool>
{
    public Task<bool> HandleAsync(
        UnsubscribeFromPushCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return subscriptions.UnsubscribeAsync(command.Slug, command.Endpoint, cancellationToken);
    }
}
