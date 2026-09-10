using FluentValidation;

using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Accounts;

/// <summary>
/// Namnet på kontot (`#154`).
///
/// <h3>Varför servern lärde sig namn</h3>
///
/// <para>
/// Fram till nu lagrade servern bara en mejladress, och samåkningen blev därmed räknad i
/// stället för namngiven: "någon frågar om skjuts". Mellan grannar som möts på planen
/// nästa lördag är det ett tomrum — och tränarens överblick kunde inte svara på vem som
/// kör.
/// </para>
///
/// <h3>Vad namnet inte får bli</h3>
///
/// <para>
/// Det är en personuppgift om en <b>vuxen</b> — §KM.1:s tak gäller barn och berörs inte.
/// Men det ska aldrig nå en gäst (§KM.3), aldrig hamna i en logg eller en audit-rad
/// (§KM.10), och det försvinner med kontot (§KM.6).
/// </para>
/// </summary>
public sealed record AccountProfileDto(string? FirstName, string? LastName, string? DisplayName)
{
    /// <summary>Sant när kontot ännu inte fyllt i något. Gränssnittet frågar då efter det.</summary>
    public bool NeedsName => string.IsNullOrWhiteSpace(FirstName);
}

/// <summary>Kontots eget namn. Bara den inloggade kan fråga efter sitt.</summary>
public sealed record GetAccountProfileQuery(Guid AccountId) : IQuery<AccountProfileDto?>;

/// <summary>Sätter eller ändrar namnet.</summary>
public sealed record UpdateAccountNameCommand(Guid AccountId, string FirstName, string? LastName)
    : ICommand<AccountProfileDto?>;

internal sealed class UpdateAccountNameCommandValidator
    : AbstractValidator<UpdateAccountNameCommand>
{
    /// <summary>Samma tak som kolumnen. Ett namn som inte får plats är inget namn.</summary>
    internal const int MaxNameLength = 60;

    public UpdateAccountNameCommandValidator()
    {
        RuleFor(c => c.AccountId).NotEmpty();

        /*
         * Fornamnet kravs, efternamnet inte. I ett foraldralag racker fornamnet nastan
         * alltid, och det som inte behovs ska inte krävas -- men den som vill skilja tva
         * Anna at ska kunna gora det.
         */
        RuleFor(c => c.FirstName)
            .NotEmpty().WithMessage("Skriv ditt förnamn.")
            .MaximumLength(MaxNameLength).WithMessage("Förnamnet är för långt.");

        RuleFor(c => c.LastName)
            .MaximumLength(MaxNameLength).WithMessage("Efternamnet är för långt.");
    }
}

internal sealed class GetAccountProfileQueryHandler(IAccountRepository accounts)
    : IQueryHandler<GetAccountProfileQuery, AccountProfileDto?>
{
    public async Task<AccountProfileDto?> HandleAsync(
        GetAccountProfileQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var account = await accounts.FindByIdAsync(query.AccountId, cancellationToken)
            .ConfigureAwait(false);

        return account is null
            ? null
            : new AccountProfileDto(account.FirstName, account.LastName, account.DisplayName);
    }
}

internal sealed class UpdateAccountNameCommandHandler(
    IAccountRepository accounts,
    IAuditLog audit) : ICommandHandler<UpdateAccountNameCommand, AccountProfileDto?>
{
    public async Task<AccountProfileDto?> HandleAsync(
        UpdateAccountNameCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await accounts.FindByIdAsync(command.AccountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return null;
        }

        account.FirstName = command.FirstName.Trim();
        account.LastName = Blank(command.LastName);

        /*
         * Audit-raden bar att namnet andrades och vems konto det galler -- aldrig vad det
         * andrades till (§KM.10). En auditlogg som skrev ut namnet hade gjort raderingen av
         * kontot ofullstandig: raden ligger kvar aven nar kontot ar borta.
         */
        await audit.RecordAsync(AuditActions.AccountNameChanged, account.Id, cancellationToken)
            .ConfigureAwait(false);

        await accounts.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new AccountProfileDto(account.FirstName, account.LastName, account.DisplayName);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
