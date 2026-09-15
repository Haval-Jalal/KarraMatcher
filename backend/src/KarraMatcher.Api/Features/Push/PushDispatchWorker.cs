using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;

namespace KarraMatcher.Api.Features.Push;

/// <summary>
/// Tömmer utkorgen och skickar (`#61`).
///
/// <h3>Aldrig i en request</h3>
///
/// <para>
/// En tränare som flyttar en match ska få sitt svar direkt. Femton utskick över internet
/// tar sekunder i bästa fall och hänger sig i värsta — och Render har en kallstart att
/// betala för i förväg (§KM.11). Handlern köar därför bara, och den här tjänsten gör resten.
/// </para>
///
/// <h3>En död prenumeration fäller inte utskicket till alla andra</h3>
///
/// <para>
/// Varje mottagare hanteras för sig. Svarar push-tjänsten att en prenumeration är borta
/// samlas den upp och raderas efteråt, i en enda fråga — resten av laget får sin notis som
/// vanligt.
/// </para>
///
/// <h3>Backoff, och sedan tystnad</h3>
///
/// <para>
/// Tillfälliga fel görs om, med växande paus: en sekund, fem, tjugofem. Går det inte då är
/// det inte tillfälligt, och en notis om lördagens match är värdelös på söndagen. Att
/// försöka i all evighet hade dessutom byggt en kö som aldrig tar slut.
/// </para>
/// </summary>
internal sealed partial class PushDispatchWorker(
    IPushOutboxReader outbox,
    IServiceScopeFactory scopes,
    ILogger<PushDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan[] Backoff =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(25),
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var dispatch in outbox.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                await SendAllAsync(dispatch, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal nedstangning.
        }
    }

    private async Task SendAllAsync(PushDispatch dispatch, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<IPushDeliveryRepository>();
            var sender = scope.ServiceProvider.GetRequiredService<IPushSender>();

            // Hela laget (AccountIds == null) eller en handfull konton -- och alltid filtrerat
            // pa vad mottagaren valt for den har sortens notis och det har laget (#65). Ett
            // tomt konto-set ar inte ett fel: ingen av de inblandade hade en enhet registrerad.
            var targets = dispatch.AccountIds is null
                ? await repository
                    .ListForTeamAsync(dispatch.TeamId, dispatch.Category, cancellationToken)
                    .ConfigureAwait(false)
                : dispatch.AccountIds is { Count: > 0 } accounts
                    ? await repository
                        .ListForAccountsAsync(dispatch.TeamId, accounts, dispatch.Category, cancellationToken)
                        .ConfigureAwait(false)
                    : [];

            if (targets.Count == 0)
            {
                return;
            }

            var delivered = new List<Guid>();
            var gone = new List<Guid>();

            foreach (var target in targets)
            {
                var outcome = await SendWithRetryAsync(
                    sender, target, dispatch.Message, cancellationToken).ConfigureAwait(false);

                switch (outcome)
                {
                    case PushOutcome.Delivered:
                        delivered.Add(target.Id);
                        break;

                    case PushOutcome.Gone:
                        gone.Add(target.Id);
                        break;

                    default:
                        break;
                }
            }

            await repository.MarkDeliveredAsync(delivered, cancellationToken).ConfigureAwait(false);
            await repository.RemoveAsync(gone, cancellationToken).ConfigureAwait(false);

            // Antal, aldrig adresser (§KM.10).
            LogDispatched(logger, delivered.Count, targets.Count, gone.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            /*
             * En trasig notis far inte falla tjansten. Gjorde den det skulle nasta
             * matchandring ga ut i tomma intet, och felet skulle se ut som att push aldrig
             * fungerat -- i stallet for som ett enskilt misslyckat utskick.
             */
            LogDispatchFailed(logger, exception);
        }
    }

    private static async Task<PushOutcome> SendWithRetryAsync(
        IPushSender sender,
        PushTarget target,
        PushMessage message,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            var outcome = await sender.SendAsync(target, message, cancellationToken).ConfigureAwait(false);

            if (outcome != PushOutcome.Retry || attempt >= Backoff.Length)
            {
                return outcome;
            }

            await Task.Delay(Backoff[attempt], cancellationToken).ConfigureAwait(false);
        }
    }

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Notiser skickade: {Delivered} av {Total}, {Gone} doda prenumerationer borttagna.")]
    private static partial void LogDispatched(ILogger logger, int delivered, int total, int gone);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Error,
        Message = "Ett notisutskick misslyckades helt. Ovriga utskick paverkas inte.")]
    private static partial void LogDispatchFailed(ILogger logger, Exception exception);
}
