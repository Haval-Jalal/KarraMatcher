namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Rensar gamla chattmeddelanden (§KM.10, `#201`).</summary>
public interface IChatRetentionRepository
{
    /// <summary>
    /// Tar bort meddelanden vars publiceringstid är äldre än <paramref name="cutoffUtc"/>, och
    /// deras anmälningar. Läser bara id:n — texten lämnar aldrig databasen. Ger antalet raderade.
    /// </summary>
    public Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
