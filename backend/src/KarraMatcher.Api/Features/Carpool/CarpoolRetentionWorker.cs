using KarraMatcher.Application.Features.Carpool;

namespace KarraMatcher.Api.Features.Carpool;

/// <summary>
/// Kör gallringen av samåkning (§KM.12, säkerhetschecklistan 9.8).
///
/// <h3>Varför den bor i processen och inte i en cron</h3>
///
/// <para>
/// Vercel Hobby tillåter ett cronjobb per dygn, och det är redan lovat bort till
/// påminnelsen kvällen före match (§KM.11). Ett andra schemalagt anrop hade alltså krävt
/// en till plattformsdel — och en gallring som beror på att någon annan tjänst hör av sig
/// är en gallring som tyst slutar köra den dag den tjänsten byter villkor.
/// </para>
///
/// <h3>Kallstarten är en tillgång här</h3>
///
/// <para>
/// Render free somnar efter en kvarts tystnad och väcks av uppetidsverktyget. Varje
/// uppvaknande kör alltså gallringen på nytt, och dygnsintervallet är bara för den process
/// som råkar leva länge. Att den körs oftare än nödvändigt gör ingen skada: raderingen är
/// idempotent, och när det inte finns något att ta bort kostar den en fråga.
/// </para>
///
/// <h3>Ett fel här får inte fälla API:t</h3>
///
/// <para>
/// En ohanterad exception i en <c>BackgroundService</c> stänger hela värden som standard.
/// Att matchschemat slutar svara för att en städning misslyckades vore fel proportioner —
/// felet loggas och nästa varv får försöka igen.
/// </para>
/// </summary>
internal sealed partial class CarpoolRetentionWorker(
    IServiceScopeFactory scopes,
    ILogger<CarpoolRetentionWorker> logger) : BackgroundService
{
    /// <summary>Så ofta gallringen körs i en process som får leva.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// Andrum vid start, så att den första förfrågan efter en kallstart inte behöver dela
    /// databasanslutning med en städning.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                await PurgeAsync(stoppingToken).ConfigureAwait(false);

                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal nedstängning. Inget att rapportera.
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();

            var retention = scope.ServiceProvider.GetRequiredService<CarpoolRetentionService>();

            await retention.PurgeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPurgeFailed(logger, exception);
        }
    }

    /// <summary>Källgenererad loggrad. Undantaget bär ingen fritext från en förälder.</summary>
    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Error,
        Message = "Gallringen av samakning misslyckades. Forsoker igen nasta varv.")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);
}
