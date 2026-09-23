using KarraMatcher.Application.Features.Attendance;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Kör gallringen av kallelser (§KM.7/§KM.10, `#203`). Samma mönster som samåkningens och
/// chattens gallring: i processen (inte cron — det enda dygns-cronjobbet är lovat till
/// match-påminnelsen), körs på varje uppvaknande efter en kallstart, idempotent, och ett fel
/// fäller aldrig API:t.
/// </summary>
internal sealed partial class AttendanceRetentionWorker(
    IServiceScopeFactory scopes,
    ILogger<AttendanceRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(50);

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
            // Normal nedstängning.
        }
    }

    private async Task PurgeAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();

            var retention = scope.ServiceProvider.GetRequiredService<AttendanceRetentionService>();

            await retention.PurgeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPurgeFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventId = 1103,
        Level = LogLevel.Error,
        Message = "Gallringen av kallelser misslyckades. Forsoker igen nasta varv.")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);
}
