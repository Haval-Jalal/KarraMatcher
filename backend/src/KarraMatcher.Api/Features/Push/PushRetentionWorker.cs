using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Api.Features.Push;

/// <summary>
/// Kör gallringen av tysta push-prenumerationer (`#68`, säkerhetschecklistan 9.8).
///
/// <h3>Varför den bor i processen och inte i en cron</h3>
///
/// <para>
/// Samma skäl som samåkningens gallring (<c>CarpoolRetentionWorker</c>): Vercel Hobby ger
/// ett cronjobb per dygn, och det är lovat bort till påminnelsen kvällen före match
/// (§KM.11). En gallring som beror på att någon annan tjänst hör av sig är en gallring som
/// tyst slutar köra den dagen tjänsten byter villkor.
/// </para>
///
/// <h3>Kallstarten är en tillgång</h3>
///
/// <para>
/// Render free somnar och väcks flera gånger om dagen av uppetidsverktyget. Varje
/// uppvaknande kör gallringen på nytt; dygnsintervallet är bara för den process som råkar
/// leva länge. Att den körs oftare än nödvändigt gör ingen skada — raderingen är idempotent,
/// och utan något att ta bort kostar den en fråga.
/// </para>
///
/// <h3>Ett fel här får inte fälla API:t</h3>
///
/// <para>
/// En ohanterad exception i en <c>BackgroundService</c> stänger annars hela värden. Att
/// matchschemat slutar svara för att en städning misslyckades vore fel proportioner — felet
/// loggas och nästa varv får försöka igen.
/// </para>
/// </summary>
internal sealed partial class PushRetentionWorker(
    IServiceScopeFactory scopes,
    ILogger<PushRetentionWorker> logger) : BackgroundService
{
    /// <summary>Så ofta gallringen körs i en process som får leva.</summary>
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>
    /// Andrum vid start, så att den första förfrågan efter en kallstart inte behöver dela
    /// databasanslutning med en städning.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

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

            var retention = scope.ServiceProvider.GetRequiredService<PushRetentionService>();

            await retention.PurgeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPurgeFailed(logger, exception);
        }
    }

    /// <summary>Källgenererad loggrad. Undantaget bär ingen endpoint (§KM.10).</summary>
    [LoggerMessage(
        EventId = 1104,
        Level = LogLevel.Error,
        Message = "Gallringen av push-prenumerationer misslyckades. Forsoker igen nasta varv.")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);
}
