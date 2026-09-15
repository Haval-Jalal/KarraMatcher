using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Gallringen: en push-prenumeration som varit tyst i tolv månader raderas (`#68`,
/// säkerhetschecklistan 9.8).
///
/// <h3>Vad den fångar som den reaktiva städningen inte gör</h3>
///
/// <para>
/// En prenumeration vars push-tjänst svarar 404/410 tas redan bort där och då, när ett
/// utskick träffar den (<c>PushDispatchWorker</c>). Men en telefon som lagts undan får inga
/// utskick att svara på — den prenumerationen dör tyst och skulle ligga kvar för alltid.
/// Den här sveper upp den: en teknisk adress till en webbläsare är en personuppgift
/// (§KM.10), och en som ingen längre använder ska inte sparas.
/// </para>
///
/// <h3>Vad som loggas</h3>
///
/// <para>
/// Antal rader, aldrig en endpoint (§KM.10). En gallringslogg som skrev ut vad den tog bort
/// hade flyttat problemet till loggfilen, som ofta lever längre än databasraden.
/// </para>
/// </summary>
public sealed partial class PushRetentionService(
    IPushRetentionRepository repository,
    TimeProvider clock,
    ILogger<PushRetentionService> logger)
{
    /// <summary>
    /// Hur länge en prenumeration får vara tyst innan den gallras.
    ///
    /// <para>
    /// Tolv månader: en säsong plus marginal. Appen används säsongsvis, så en prenumeration
    /// som varit tyst över en hel säsong hör till en enhet som inte längre lyssnar.
    /// </para>
    /// </summary>
    public static TimeSpan InactivePeriod { get; } = TimeSpan.FromDays(365);

    /// <summary>Gallrar de tysta. Säker att köra hur ofta som helst — raderingen är idempotent.</summary>
    public async Task<int> PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - InactivePeriod;

        var removed = await repository.PurgeInactiveAsync(cutoff, cancellationToken)
            .ConfigureAwait(false);

        if (removed > 0)
        {
            LogPurged(logger, removed, cutoff);
        }

        return removed;
    }

    /// <summary>Källgenererad loggrad. Bara antalet och gränsen — aldrig en endpoint (§KM.10).</summary>
    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Information,
        Message = "Gallrade push-prenumerationer: {Removed} tysta sedan fore {Cutoff:O}.")]
    private static partial void LogPurged(ILogger logger, int removed, DateTime cutoff);
}
