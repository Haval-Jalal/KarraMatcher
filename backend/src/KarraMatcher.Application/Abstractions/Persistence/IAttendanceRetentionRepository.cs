namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Rensar gamla kallelser och deras svar (§KM.7/§KM.10, `#203`).</summary>
public interface IAttendanceRetentionRepository
{
    /// <summary>
    /// Tar bort kallelser (och deras per-barn-svar) för händelser vars starttid är äldre än
    /// <paramref name="cutoffUtc"/>. Läser bara id:n. Ger antalet raderade kallelser.
    /// </summary>
    public Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken);
}
