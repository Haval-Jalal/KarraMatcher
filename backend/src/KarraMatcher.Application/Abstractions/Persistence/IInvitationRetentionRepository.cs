namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Gallrar döda inbjudningar (§KM.6, `#585`).</summary>
public interface IInvitationRetentionRepository
{
    /// <summary>
    /// Tar bort inbjudningar som inte accepterats och vars <c>ExpiresUtc</c> är äldre än
    /// <paramref name="cutoffUtc"/> — en utgången väntande eller en återkallad inbjudan vars
    /// adress ingen längre behöver. Läser bara id:n, så adressen hämtas aldrig till minnet
    /// (§KM.10). En <em>accepterad</em> inbjudan lämnas: den hör till ett levande konto och
    /// kaskaderar bort med det. Ger antalet raderade.
    /// </summary>
    public Task<int> PurgeDeadOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
