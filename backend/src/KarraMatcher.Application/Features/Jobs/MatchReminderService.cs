using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Matches.Admin;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Common;

namespace KarraMatcher.Application.Features.Jobs;

/// <summary>
/// Kvällspåminnelsen om morgondagens matcher (`#64`, §KM.11).
///
/// <h3>En gång per dygn, körd utifrån</h3>
///
/// <para>
/// Render sover efter en kvart, och kvällen före match är precis en sådan tyst stund.
/// Därför väcks jobbet av Vercels cron via en skyddad endpoint — inte av en timer i
/// processen, som inte skulle vakna. Att Vercel Hobby bara tillåter en cron per dygn räcker
/// exakt: påminnelsen ska gå en gång, kvällen före.
/// </para>
///
/// <h3>"I morgon" är ett svenskt dygn</h3>
///
/// <para>
/// Klubben spelar i svensk tid, databasen lagrar UTC. "Morgondagens matcher" är därför de
/// vars avspark faller på morgondagens svenska datum — omräknat till ett UTC-fönster på ett
/// enda ställe (§KM.5), så oktoberskiftet inte gör att en lördagsmatch missas eller påminns
/// om en dag fel.
/// </para>
///
/// <h3>Idempotent</h3>
///
/// <para>
/// Bara matcher som ännu inte påmints om tas med, och de märks efteråt. En andra körning
/// samma kväll hittar dem inte längre — ingen förälder får två notiser för samma match.
/// </para>
/// </summary>
public sealed class MatchReminderService(
    IMatchReminderRepository matches,
    IPushOutbox push,
    TimeProvider clock)
{
    /// <summary>Skickar påminnelser om morgondagens matcher. Ger antalet matcher som påmindes.</summary>
    public async Task<int> SendDueRemindersAsync(CancellationToken cancellationToken)
    {
        var nowSwedish = SwedishTime.ToSwedish(clock.GetUtcNow().UtcDateTime);
        var tomorrow = DateOnly.FromDateTime(nowSwedish).AddDays(1);

        var fromUtc = SwedishTime.ToUtc(tomorrow, TimeOnly.MinValue);
        var toUtc = SwedishTime.ToUtc(tomorrow.AddDays(1), TimeOnly.MinValue);

        var due = await matches.ListDueAsync(fromUtc, toUtc, cancellationToken).ConfigureAwait(false);

        if (due.Count == 0)
        {
            return 0;
        }

        foreach (var match in due)
        {
            push.Enqueue(PushDispatch.ToTeam(match.TeamId, MatchNotification.Reminder(match)));
        }

        // Märks efter att notiserna köats -- en andra körning hittar dem inte längre.
        await matches
            .MarkRemindedAsync(
                [.. due.Select(m => m.MatchId)],
                clock.GetUtcNow().UtcDateTime,
                cancellationToken)
            .ConfigureAwait(false);

        return due.Count;
    }
}
