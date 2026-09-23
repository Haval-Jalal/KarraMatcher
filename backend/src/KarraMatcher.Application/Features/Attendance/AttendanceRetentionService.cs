using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Application.Features.Attendance;

/// <summary>
/// Gallrar gamla kallelser (§KM.7/§KM.10, `#203`).
///
/// <para>
/// En kallelse säger inget längre när händelsen är förbi. Kallelser (och deras per-barn-svar)
/// för händelser vars starttid är äldre än <see cref="RetentionPeriod"/> tas bort — samma
/// 30-dagarsregel som samåkningen, båda förankrade i händelsens starttid. Bara antal loggas.
/// </para>
/// </summary>
public sealed partial class AttendanceRetentionService(
    IAttendanceRetentionRepository repository,
    TimeProvider clock,
    ILogger<AttendanceRetentionService> logger)
{
    /// <summary>Hur länge en kallelse sparas efter händelsen innan den gallras (30 dagar, `#203`).</summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(30);

    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - RetentionPeriod;

        var purged = await repository.PurgeOlderThanAsync(cutoff, cancellationToken)
            .ConfigureAwait(false);

        if (purged > 0)
        {
            LogPurged(purged);
        }

        return purged;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Gallrade {Count} kallelser.")]
    private partial void LogPurged(int count);
}
