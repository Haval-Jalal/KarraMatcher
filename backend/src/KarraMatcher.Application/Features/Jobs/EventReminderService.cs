using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Events.Admin;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Common;

namespace KarraMatcher.Application.Features.Jobs;

/// <summary>
/// Kvällspåminnelsen om morgondagens händelser (`#64`, `#198`, §KM.11).
///
/// <h3>En gång per dygn, körd utifrån</h3>
///
/// <para>
/// Render sover efter en kvart, och kvällen före är precis en sådan tyst stund. Därför väcks
/// jobbet av Vercels cron via en skyddad endpoint — inte av en timer i processen, som inte
/// skulle vakna. Att Vercel Hobby bara tillåter en cron per dygn räcker exakt.
/// </para>
///
/// <h3>"I morgon" är ett svenskt dygn</h3>
///
/// <para>
/// Klubben spelar i svensk tid, databasen lagrar UTC. "Morgondagens händelser" är därför de
/// vars start faller på morgondagens svenska datum — omräknat till ett UTC-fönster på ett
/// enda ställe (§KM.5), så oktoberskiftet inte gör att en lördagshändelse missas.
/// </para>
///
/// <h3>Idempotent</h3>
///
/// <para>
/// Bara händelser som ännu inte påmints om tas med, och de märks efteråt. En andra körning
/// samma kväll hittar dem inte längre — ingen förälder får två notiser för samma händelse.
/// </para>
/// </summary>
public sealed class EventReminderService(
    IEventReminderRepository events,
    IPushOutbox push,
    TimeProvider clock)
{
    /// <summary>Skickar påminnelser om morgondagens händelser. Ger antalet som påmindes.</summary>
    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var nowSwedish = SwedishTime.ToSwedish(clock.GetUtcNow().UtcDateTime);
        var tomorrow = DateOnly.FromDateTime(nowSwedish).AddDays(1);

        var fromUtc = SwedishTime.ToUtc(tomorrow, TimeOnly.MinValue);
        var toUtc = SwedishTime.ToUtc(tomorrow.AddDays(1), TimeOnly.MinValue);

        var due = await events.ListDueAsync(fromUtc, toUtc, cancellationToken).ConfigureAwait(false);

        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var item in due)
        {
            push.Enqueue(PushDispatch.ToTeam(
                item.TeamId, PushCategory.EventChange, EventNotification.Reminder(item)));
        }

        // Märks efter att notiserna köats -- en andra körning hittar dem inte längre.
        await events
            .MarkRemindedAsync(
                [.. due.Select(item => item.EventId)],
                clock.GetUtcNow().UtcDateTime,
                cancellationToken)
            .ConfigureAwait(false);

        return due.Count;
    }
}
