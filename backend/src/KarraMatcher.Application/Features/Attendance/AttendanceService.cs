using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Attendance;

/// <summary>Vad ett försök att skicka/ändra en kallelse slutade med.</summary>
public enum SetKallelseOutcome
{
    /// <summary>Kallelsen skickades eller uppdaterades.</summary>
    Set = 0,

    /// <summary>Händelsen finns inte eller hör till en annan trupp.</summary>
    EventNotInTrupp = 1,

    /// <summary>Kallelser är inte påslagna för händelsens lag (§KM.7-grinden).</summary>
    Disabled = 2,

    /// <summary>Ett valt barn hör inte till truppen.</summary>
    InvalidChild = 3,
}

/// <summary>Vad ett försök att svara på en kallelse slutade med.</summary>
public enum RespondOutcome
{
    /// <summary>Svaret sparades.</summary>
    Saved = 0,

    /// <summary>Händelsen finns inte, eller ingen kallelse har öppnats.</summary>
    NotCalled = 1,

    /// <summary>Den inloggade är inte vårdnadshavare för barnet.</summary>
    NotGuardian = 2,

    /// <summary>Barnet är inte kallat till den här händelsen.</summary>
    NotInvited = 3,

    /// <summary>Händelsen har redan börjat — ett svar säger ingenting längre.</summary>
    Closed = 4,
}

/// <summary>
/// Den riktade kallelsen per barn (§KM.7, `#199`).
///
/// <h3>Kallas ur hela truppen, av admin</h3>
///
/// <para>
/// En match spelas av ett färg-lag, men laget fylls vid behov på med barn ur de andra lagen.
/// Därför riktas kallelsen mot <em>utvalda barn i truppen</em>, inte mot ett lag, och sköts av
/// en admin för truppen (alla P16-tränare är admins). Varje valt barn prövas höra till truppen.
/// </para>
///
/// <h3>Svar per barn, Ja/Nej</h3>
///
/// <para>
/// En vårdnadshavare svarar för sina egna barn — Ja eller Nej. Objektnivå: svaret prövas mot
/// <see cref="IAttendanceCallRepository.IsGuardianOfChildAsync"/> och mot att barnet är kallat.
/// Barn visas som "Liam J" (§KM.1); svaren audit-loggas inte (många, ändras ofta), men att en
/// kallelse öppnats loggas (§KM.10).
/// </para>
/// </summary>
public sealed class AttendanceService(
    IAttendanceCallRepository calls,
    AttendanceGate gate,
    IAuditLog audit,
    IPushOutbox push)
{
    /// <summary>Skickar eller uppdaterar kallelsen: vilka barn som är kallade till händelsen.</summary>
    public async Task<SetKallelseOutcome> SetKallelseAsync(
        Guid truppId,
        Guid eventId,
        IReadOnlyCollection<Guid> childIds,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(childIds);

        var context = await calls.FindEventContextAsync(eventId, cancellationToken)
            .ConfigureAwait(false);

        if (context is null || context.AgeGroupId != truppId)
        {
            return SetKallelseOutcome.EventNotInTrupp;
        }

        if (!await gate.IsEnabledForMatchAsync(eventId, cancellationToken).ConfigureAwait(false))
        {
            return SetKallelseOutcome.Disabled;
        }

        var truppChildren = await calls.ChildIdsInTruppAsync(context.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);

        var desired = childIds.ToHashSet();

        if (desired.Any(id => !truppChildren.Contains(id)))
        {
            return SetKallelseOutcome.InvalidChild;
        }

        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (call is null)
        {
            call = new AttendanceCall
            {
                Id = Guid.NewGuid(),
                MatchId = eventId,
                OpenedByAccountId = actorAccountId,
                OpenedUtc = DateTime.UtcNow,
            };

            await calls.AddCallAsync(call, cancellationToken).ConfigureAwait(false);

            await audit.RecordAsync(
                AuditActions.AttendanceCallOpened, actorAccountId, cancellationToken, eventId)
                .ConfigureAwait(false);
        }

        var existing = await calls.ListInvitationsAsync(call.Id, cancellationToken)
            .ConfigureAwait(false);
        var existingIds = existing.Select(i => i.ChildId).ToHashSet();

        // Ta bort barn som avmarkerats (deras ev. svar följer med bort — de är inte kallade längre).
        foreach (var invitation in existing.Where(i => !desired.Contains(i.ChildId)))
        {
            calls.RemoveInvitation(invitation);
        }

        // Lägg till nyvalda barn (utan svar än). Redan kallade barn behåller sitt svar.
        var added = desired.Where(id => !existingIds.Contains(id)).ToArray();

        foreach (var childId in added)
        {
            await calls.AddInvitationAsync(
                new AttendanceInvitation
                {
                    Id = Guid.NewGuid(),
                    CallId = call.Id,
                    ChildId = childId,
                },
                cancellationToken).ConfigureAwait(false);
        }

        await calls.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Notis till de nykallade barnens vårdnadshavare — aldrig barnets namn (§KM.1).
        if (added.Length > 0)
        {
            var guardians = await calls
                .GuardianAccountIdsForChildrenAsync(added, cancellationToken)
                .ConfigureAwait(false);

            if (guardians.Count > 0)
            {
                push.Enqueue(PushDispatch.ToAccounts(
                    context.TeamId,
                    guardians,
                    PushCategory.MatchChange,
                    new PushMessage(
                        "Ny kallelse",
                        "Ditt barn är kallat. Öppna för att svara Ja eller Nej.",
                        $"/handelse/{eventId}")));
            }
        }

        return SetKallelseOutcome.Set;
    }

    /// <summary>Sparar en vårdnadshavares Ja/Nej för ett av sina barn.</summary>
    public async Task<RespondOutcome> RespondAsync(
        Guid eventId,
        Guid childId,
        Guid accountId,
        AttendanceReply reply,
        CancellationToken cancellationToken)
    {
        var kickoff = await calls.FindKickoffUtcAsync(eventId, cancellationToken).ConfigureAwait(false);
        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (kickoff is null || call is null)
        {
            return RespondOutcome.NotCalled;
        }

        if (!await calls.IsGuardianOfChildAsync(accountId, childId, cancellationToken)
            .ConfigureAwait(false))
        {
            return RespondOutcome.NotGuardian;
        }

        var invitation = await calls.FindInvitationAsync(call.Id, childId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation is null)
        {
            return RespondOutcome.NotInvited;
        }

        if (kickoff.Value <= DateTime.UtcNow)
        {
            return RespondOutcome.Closed;
        }

        invitation.Reply = reply;
        invitation.RespondedByAccountId = accountId;
        invitation.RespondedUtc = DateTime.UtcNow;

        await calls.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return RespondOutcome.Saved;
    }

    /// <summary>Vårdnadshavarens vy: den inloggades egna kallade barn för händelsen.</summary>
    public async Task<MyKallelseDto?> GetMineAsync(
        Guid eventId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var kickoff = await calls.FindKickoffUtcAsync(eventId, cancellationToken).ConfigureAwait(false);

        // Osynlig tills klubben slår på kallelsen för laget (§KM.7): saknad grind → null → 404.
        if (kickoff is null
            || !await gate.IsEnabledForMatchAsync(eventId, cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);
        var mine = call is null
            ? []
            : await calls.ListMineAsync(eventId, accountId, cancellationToken).ConfigureAwait(false);

        return new MyKallelseDto(
            call is not null,
            new DateTimeOffset(kickoff.Value, TimeSpan.Zero),
            [.. mine.Select(m => new MyChildInvitationDto(
                m.ChildId, DisplayName(m.FirstName, m.LastInitial), m.Reply?.ToString()))]);
    }

    /// <summary>Adminens sammanställning. Null när händelsen inte hör till truppen.</summary>
    public async Task<KallelseSummaryDto?> GetSummaryAsync(
        Guid truppId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var context = await calls.FindEventContextAsync(eventId, cancellationToken)
            .ConfigureAwait(false);

        if (context is null || context.AgeGroupId != truppId)
        {
            return null;
        }

        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        var rows = call is null
            ? []
            : await calls.ListInvitationRowsAsync(call.Id, cancellationToken).ConfigureAwait(false);

        return new KallelseSummaryDto(
            call is not null,
            rows.Count(r => r.Reply == AttendanceReply.Coming),
            rows.Count(r => r.Reply == AttendanceReply.NotComing),
            rows.Count(r => r.Reply is null),
            [.. rows.Select(r => new KallelseChildDto(
                r.ChildId,
                DisplayName(r.FirstName, r.LastInitial),
                r.TeamName,
                r.ColorHex,
                r.Reply?.ToString()))]);
    }

    /// <summary>
    /// Påminner vårdnadshavarna till de kallade barn som ännu inte svarat (§KM.7). Null när
    /// händelsen inte hör till truppen; annars antalet barn som saknar svar.
    /// </summary>
    public async Task<int?> RemindNonRespondersAsync(
        Guid truppId,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var context = await calls.FindEventContextAsync(eventId, cancellationToken)
            .ConfigureAwait(false);

        if (context is null || context.AgeGroupId != truppId)
        {
            return null;
        }

        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (call is null)
        {
            return 0;
        }

        var rows = await calls.ListInvitationRowsAsync(call.Id, cancellationToken)
            .ConfigureAwait(false);
        var notAnswered = rows.Where(r => r.Reply is null).Select(r => r.ChildId).ToArray();

        if (notAnswered.Length > 0)
        {
            var guardians = await calls
                .GuardianAccountIdsForChildrenAsync(notAnswered, cancellationToken)
                .ConfigureAwait(false);

            if (guardians.Count > 0)
            {
                push.Enqueue(PushDispatch.ToAccounts(
                    context.TeamId,
                    guardians,
                    PushCategory.Reminder,
                    new PushMessage(
                        "Påminnelse: svara på kallelsen",
                        "Kommer ditt barn? Öppna för att svara Ja eller Nej.",
                        $"/handelse/{eventId}")));
            }
        }

        return notAnswered.Length;
    }

    private static string DisplayName(string firstName, string lastInitial) =>
        $"{firstName} {lastInitial}".Trim();
}
