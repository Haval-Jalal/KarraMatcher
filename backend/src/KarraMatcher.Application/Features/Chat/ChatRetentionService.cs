using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Application.Features.Chat;

/// <summary>
/// Gallrar gamla chattmeddelanden (§KM.10, `#201`).
///
/// <para>
/// Chatt är potentiell PII och sparas inte för evigt. Meddelanden vars publiceringstid är
/// äldre än <see cref="RetentionPeriod"/> tas bort tillsammans med sina anmälningar. Bara
/// antal loggas — aldrig innehåll.
/// </para>
/// </summary>
public sealed partial class ChatRetentionService(
    IChatRetentionRepository repository,
    TimeProvider clock,
    ILogger<ChatRetentionService> logger)
{
    /// <summary>Hur länge ett meddelande sparas innan det gallras (90 dagar, `#201`).</summary>
    public static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(90);

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Gallrade {Count} chattmeddelanden.")]
    private partial void LogPurged(int count);
}
