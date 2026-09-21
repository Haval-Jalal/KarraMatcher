using KarraMatcher.Application.Features.Chat;

namespace KarraMatcher.Api.Features.Chat;

/// <summary>
/// Kör gallringen av chatt (§KM.10, `#201`). Samma mönster som samåkningens gallring: i
/// processen (inte i cron — det enda dygns-cronjobbet är lovat till match-påminnelsen), körs
/// på varje uppvaknande efter en kallstart, och ett fel fäller aldrig API:t.
/// </summary>
internal sealed partial class ChatRetentionWorker(
    IServiceScopeFactory scopes,
    ILogger<ChatRetentionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(40);

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

            var retention = scope.ServiceProvider.GetRequiredService<ChatRetentionService>();

            await retention.PurgeAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogPurgeFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Error,
        Message = "Gallringen av chatt misslyckades. Forsoker igen nasta varv.")]
    private static partial void LogPurgeFailed(ILogger logger, Exception exception);
}
