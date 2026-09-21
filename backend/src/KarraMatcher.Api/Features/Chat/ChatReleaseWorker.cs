using KarraMatcher.Application.Features.Chat;

namespace KarraMatcher.Api.Features.Chat;

/// <summary>
/// Släpper schemalagda chattmeddelanden vars tid passerat (`#201`).
///
/// <para>
/// Kör ofta (varje minut) i processen, så ett schemalagt meddelande går ut nära sin tid.
/// <b>Ärlig begränsning:</b> Render free somnar efter ~15 min tystnad — sover processen när
/// tiden passerar släpps meddelandet först när den vaknar (av uppetids-pingen eller en
/// förfrågan), alltså oftast på minuten men möjligen några minuter sent. Släppet är
/// idempotent (bara opublicerade vars tid gått), och ett fel fäller aldrig API:t.
/// </para>
/// </summary>
internal sealed partial class ChatReleaseWorker(
    IServiceScopeFactory scopes,
    ILogger<ChatReleaseWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken).ConfigureAwait(false);

            while (!stoppingToken.IsCancellationRequested)
            {
                await ReleaseAsync(stoppingToken).ConfigureAwait(false);

                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal nedstängning.
        }
    }

    private async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();

            var chat = scope.ServiceProvider.GetRequiredService<ChatService>();

            await chat.ReleaseDueAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogReleaseFailed(logger, exception);
        }
    }

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Error,
        Message = "Slappet av schemalagda chattmeddelanden misslyckades. Forsoker igen nasta varv.")]
    private static partial void LogReleaseFailed(ILogger logger, Exception exception);
}
