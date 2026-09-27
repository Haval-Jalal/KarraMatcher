using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Cup;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Cup;

/// <summary>Vad en cup-lags-åtgärd slutade med (`#335`).</summary>
public enum CupTeamOutcome
{
    /// <summary>Åtgärden gick igenom.</summary>
    Ok = 0,

    /// <summary>Händelsen finns inte eller hör till en annan trupp.</summary>
    EventNotInTrupp = 1,

    /// <summary>Händelsen är inte en cup — bara cuper har cup-lag.</summary>
    NotACup = 2,

    /// <summary>Cup-laget finns inte, eller hör till en annan cup.</summary>
    TeamNotFound = 3,

    /// <summary>Barnet är inte anmält till cupen — bara anmälda barn kan placeras i ett lag.</summary>
    ChildNotSignedUp = 4,
}

/// <summary>Utfallet av att skapa ett cup-lag: id:t vid lyckat resultat (`#335`).</summary>
public sealed record CupTeamCreateResult(CupTeamOutcome Outcome, Guid? TeamId);

/// <summary>
/// Admin bygger cup-lag av de barn som anmält sig till en cup (`#335`, slice 4 av #330).
///
/// <para>
/// Cup-lagen är tillfälliga och per cup (§KM.1: andra barn varje gång). Bara <b>anmälda</b> barn
/// (svar Ja i den öppna anmälan, `#295`) kan placeras, och ett barn hör till <b>högst ett</b>
/// cup-lag per cup — en ny placering flyttar barnet. Behörigheten (admin för truppen) prövas i
/// endpointen; den här tjänsten vaktar objektnivån: att cupen hör till truppen, att laget hör
/// till cupen, och att barnet är anmält.
/// </para>
/// </summary>
public sealed class CupTeamService(ICupTeamRepository teams, IAttendanceCallRepository calls)
{
    /// <summary>Skapar ett tomt cup-lag i cupen.</summary>
    public async Task<CupTeamCreateResult> CreateTeamAsync(
        Guid truppId,
        Guid eventId,
        string name,
        CancellationToken cancellationToken)
    {
        var gate = await GuardCupAsync(truppId, eventId, cancellationToken).ConfigureAwait(false);

        if (gate != CupTeamOutcome.Ok)
        {
            return new CupTeamCreateResult(gate, null);
        }

        var team = new CupTeam
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Name = name.Trim(),
            CreatedUtc = DateTime.UtcNow,
        };

        await teams.AddTeamAsync(team, cancellationToken).ConfigureAwait(false);
        await teams.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CupTeamCreateResult(CupTeamOutcome.Ok, team.Id);
    }

    /// <summary>Byter namn på ett cup-lag.</summary>
    public async Task<CupTeamOutcome> RenameTeamAsync(
        Guid truppId,
        Guid eventId,
        Guid cupTeamId,
        string name,
        CancellationToken cancellationToken)
    {
        var (gate, team) = await GuardTeamAsync(truppId, eventId, cupTeamId, cancellationToken)
            .ConfigureAwait(false);

        if (gate != CupTeamOutcome.Ok)
        {
            return gate;
        }

        team!.Name = name.Trim();
        await teams.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CupTeamOutcome.Ok;
    }

    /// <summary>Tar bort ett cup-lag (dess placeringar försvinner med det, kaskad).</summary>
    public async Task<CupTeamOutcome> DeleteTeamAsync(
        Guid truppId,
        Guid eventId,
        Guid cupTeamId,
        CancellationToken cancellationToken)
    {
        var (gate, team) = await GuardTeamAsync(truppId, eventId, cupTeamId, cancellationToken)
            .ConfigureAwait(false);

        if (gate != CupTeamOutcome.Ok)
        {
            return gate;
        }

        teams.RemoveTeam(team!);
        await teams.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CupTeamOutcome.Ok;
    }

    /// <summary>
    /// Placerar ett anmält barn i ett cup-lag. Står barnet redan i ett annat av cupens lag
    /// flyttas det hit i stället för att stå i två.
    /// </summary>
    public async Task<CupTeamOutcome> AssignChildAsync(
        Guid truppId,
        Guid eventId,
        Guid cupTeamId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var (gate, team) = await GuardTeamAsync(truppId, eventId, cupTeamId, cancellationToken)
            .ConfigureAwait(false);

        if (gate != CupTeamOutcome.Ok)
        {
            return gate;
        }

        if (!await IsSignedUpAsync(eventId, childId, cancellationToken).ConfigureAwait(false))
        {
            return CupTeamOutcome.ChildNotSignedUp;
        }

        var existing = await teams
            .FindMemberByChildAsync(eventId, childId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            if (existing.CupTeamId == team!.Id)
            {
                return CupTeamOutcome.Ok; // Redan i det här laget — idempotent.
            }

            // Flytta: ta bort den gamla placeringen, lägg den nya.
            teams.RemoveMember(existing);
        }

        await teams.AddMemberAsync(
            new CupTeamMember
            {
                Id = Guid.NewGuid(),
                CupTeamId = team!.Id,
                ChildId = childId,
                CreatedUtc = DateTime.UtcNow,
            },
            cancellationToken).ConfigureAwait(false);

        await teams.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return CupTeamOutcome.Ok;
    }

    /// <summary>Tar bort ett barn ur ett cup-lag.</summary>
    public async Task<CupTeamOutcome> UnassignChildAsync(
        Guid truppId,
        Guid eventId,
        Guid cupTeamId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var (gate, _) = await GuardTeamAsync(truppId, eventId, cupTeamId, cancellationToken)
            .ConfigureAwait(false);

        if (gate != CupTeamOutcome.Ok)
        {
            return gate;
        }

        var member = await teams
            .FindMemberByChildAsync(eventId, childId, cancellationToken)
            .ConfigureAwait(false);

        // Bara om barnet faktiskt står i just det här laget. Annars är det inget att göra (idempotent).
        if (member is not null && member.CupTeamId == cupTeamId)
        {
            teams.RemoveMember(member);
            await teams.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return CupTeamOutcome.Ok;
    }

    /// <summary>Prövar att händelsen är en cup i truppen.</summary>
    private async Task<CupTeamOutcome> GuardCupAsync(
        Guid truppId, Guid eventId, CancellationToken cancellationToken)
    {
        var context = await calls.FindEventContextAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (context is null || context.AgeGroupId != truppId)
        {
            return CupTeamOutcome.EventNotInTrupp;
        }

        return context.Type != EventType.Cup ? CupTeamOutcome.NotACup : CupTeamOutcome.Ok;
    }

    /// <summary>Prövar cupen och att laget hör till den. Ger laget (spårat) vid Ok.</summary>
    private async Task<(CupTeamOutcome Outcome, CupTeam? Team)> GuardTeamAsync(
        Guid truppId, Guid eventId, Guid cupTeamId, CancellationToken cancellationToken)
    {
        var gate = await GuardCupAsync(truppId, eventId, cancellationToken).ConfigureAwait(false);

        if (gate != CupTeamOutcome.Ok)
        {
            return (gate, null);
        }

        var team = await teams.FindTeamAsync(cupTeamId, cancellationToken).ConfigureAwait(false);

        return team is null || team.EventId != eventId
            ? (CupTeamOutcome.TeamNotFound, null)
            : (CupTeamOutcome.Ok, team);
    }

    /// <summary>Är barnet anmält (svar Ja) till cupens öppna anmälan?</summary>
    private async Task<bool> IsSignedUpAsync(
        Guid eventId, Guid childId, CancellationToken cancellationToken)
    {
        var call = await calls.FindCallByEventAsync(eventId, cancellationToken).ConfigureAwait(false);

        if (call is null)
        {
            return false;
        }

        var invitation = await calls
            .FindInvitationAsync(call.Id, childId, cancellationToken)
            .ConfigureAwait(false);

        return invitation is { Reply: AttendanceReply.Coming };
    }
}
