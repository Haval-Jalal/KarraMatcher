using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.Extensions.Logging;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Gallringen: all samåkning för en match försvinner 30 dagar efter att den spelats
/// (§KM.12, säkerhetschecklistan 9.8).
///
/// <h3>Varför den är en funktion och inte en städrutin</h3>
///
/// <para>
/// Fritexten mellan föräldrar är potentiell personuppgift. Att den ligger kvar en hel
/// säsong är inte en skönhetsfläck utan ett brott mot löftet i integritetstexten — och den
/// enda tidpunkt då någon behöver den är innan matchen. Efteråt är den bara en risk.
/// </para>
///
/// <h3>Vad som loggas</h3>
///
/// <para>
/// Antal rader, aldrig innehåll (§KM.10). En gallringslogg som skrev ut vad den raderade
/// hade flyttat problemet till loggfilen i stället för att lösa det — och loggar lever
/// ofta längre än databasrader.
/// </para>
/// </summary>
public sealed partial class CarpoolRetentionService(
    ICarpoolRetentionRepository repository,
    TimeProvider clock,
    ILogger<CarpoolRetentionService> logger)
{
    /// <summary>
    /// Hur länge samåkningen får ligga kvar efter matchen.
    ///
    /// <para>
    /// Trettio dagar är inte en teknisk gräns utan ett löfte i §KM.12. Ändras talet ska det
    /// ändras i texten till föräldrarna i samma veva — annars säger appen en sak och gör en
    /// annan.
    /// </para>
    /// </summary>
    public static TimeSpan RetentionPeriod { get; } = TimeSpan.FromDays(30);

    /// <summary>Gallrar allt som passerat gränsen. Säker att köra hur ofta som helst.</summary>
    public async Task<CarpoolPurgeResult> PurgeAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime - RetentionPeriod;

        var result = await repository.PurgeAsync(cutoff, cancellationToken).ConfigureAwait(false);

        if (result.RemovedAnything)
        {
            LogPurged(logger, result.Requests, result.Offers, cutoff);
        }

        return result;
    }

    /// <summary>
    /// Källgenererad loggrad. Bara siffror och gränsen — aldrig en rads innehåll (§KM.10).
    /// </summary>
    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Information,
        Message = "Gallrade samakning: {Requests} forfragningar och {Offers} erbjudanden for matcher fore {Cutoff:O}.")]
    private static partial void LogPurged(ILogger logger, int requests, int offers, DateTime cutoff);
}
