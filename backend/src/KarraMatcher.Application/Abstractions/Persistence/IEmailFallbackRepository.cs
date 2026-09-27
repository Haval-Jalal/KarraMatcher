using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Mottagarna för e-postfallbacken: medlemmar som vill ha en kritisk notis men saknar en
/// fungerande push-prenumeration.
///
/// <para>
/// Speglar <see cref="IPushDeliveryRepository"/> men på andra sidan av gränsen — de som push
/// <em>inte</em> når. Samma filter (lagets medlemmar, kategorin inte avstängd), men i stället
/// för en prenumeration krävs frånvaron av en: har man en enhet får man push och inget mejl,
/// annars mejlet. En vuxen har alltid en e-postadress; en anonym prenumerant har inget konto
/// och finns aldrig här.
/// </para>
/// </summary>
public interface IEmailFallbackRepository
{
    /// <summary>Lagets medlemmar som inte nås av push.</summary>
    public Task<IReadOnlyList<EmailRecipient>> ListForTeamAsync(
        Guid teamId,
        PushCategory category,
        CancellationToken cancellationToken);

    /// <summary>Hela truppens medlemmar som inte nås av push — för en trupp-vid händelse (`#332`).</summary>
    public Task<IReadOnlyList<EmailRecipient>> ListForTruppAsync(
        Guid ageGroupId,
        PushCategory category,
        CancellationToken cancellationToken);

    /// <summary>Bestämda konton som inte nås av push.</summary>
    public Task<IReadOnlyList<EmailRecipient>> ListForAccountsAsync(
        IReadOnlyCollection<Guid> accountIds,
        PushCategory category,
        CancellationToken cancellationToken);
}

/// <summary>Ett konto att mejla, och adressen. Adressen loggas aldrig (§KM.10).</summary>
public sealed record EmailRecipient(Guid AccountId, string Email);
