using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Auth.DeleteAccount;

/// <summary>
/// Raderar ett konto på riktigt — inte som en markering (§KM.6).
///
/// <para>
/// Ordningen är inte godtycklig. Audit-posten skrivs <b>före</b> raderingen, i samma
/// enhet av arbete: skrivs den efteråt finns kontot inte längre att referera till, och
/// misslyckas raderingen har vi loggat något som inte hände. Nu gäller båda eller ingen.
/// </para>
///
/// <para>
/// <b>Engångskoderna är det som är lätt att glömma.</b> De hänger på adressen och inte på
/// kontot — kontot finns ju inte när koden skickas — så de följer inte med i kaskaden och
/// måste tas bort uttryckligen. Ett test vaktar det.
/// </para>
///
/// <para>
/// Spelarkortet berörs inte och kan inte beröras: det har aldrig nått servern (§KM.2).
/// Att det ligger kvar i telefonen är gränssnittets sak att förklara.
/// </para>
/// </summary>
internal sealed class DeleteAccountCommandHandler(
    IAccountRepository accounts,
    ILoginCodeRepository codes,
    IAuditLog audit) : ICommandHandler<DeleteAccountCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        DeleteAccountCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var account = await accounts.FindByIdAsync(command.AccountId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            // Redan borta. Svaret utåt är detsamma, så en upprepad radering inte ser ut
            // som ett fel för den som tryckte två gånger.
            return Unit.Value;
        }

        await audit.RecordAsync(AuditActions.AccountDeleted, account.Id, cancellationToken)
            .ConfigureAwait(false);

        await codes.DeleteForEmailAsync(account.Email, cancellationToken).ConfigureAwait(false);

        // Refresh-tokens och roller kaskaderar bort med kontot. Att de gör det är inte en
        // vana utan konfigurerat, och ett test kontrollerar att varje tabell som pekar på
        // ett konto verkligen gör det.
        accounts.Remove(account);

        await accounts.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
