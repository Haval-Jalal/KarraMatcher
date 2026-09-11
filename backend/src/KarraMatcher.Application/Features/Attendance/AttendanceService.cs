using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Audit;

namespace KarraMatcher.Application.Features.Attendance;

/// <summary>Vad ett försök att öppna en kallelse slutade med.</summary>
public enum OpenCallOutcome
{
    /// <summary>Kallelsen öppnades nu.</summary>
    Opened = 0,

    /// <summary>Den var redan öppnad. Idempotent — inget fel.</summary>
    AlreadyOpen = 1,

    /// <summary>Matchen finns inte, eller hör till ett annat lag.</summary>
    MatchNotInTeam = 2,
}

/// <summary>Vad ett försök att svara på en kallelse slutade med.</summary>
public enum SubmitResponseOutcome
{
    /// <summary>Svaret sparades.</summary>
    Saved = 0,

    /// <summary>Matchen finns inte.</summary>
    MatchNotFound = 1,

    /// <summary>Tränaren har inte kallat till matchen än.</summary>
    NotCalled = 2,

    /// <summary>Matchen har redan börjat — ett svar säger ingenting längre.</summary>
    Closed = 3,
}

/// <summary>
/// Kallelsen och närvarosvaren (`#57`, §KM.7).
///
/// <h3>Grinden vaktas här också, inte bara i attributet</h3>
///
/// <para>
/// <c>[RequireAttendanceEnabled]</c> stänger routen, men en tjänst kan anropas från något
/// annat än en controller. Att kallelsen är påslagen prövas därför både där och —
/// underförstått — här: den här tjänsten når aldrig ett anrop förbi grinden i dag, men den
/// gör inget som lämnar ut något om ett barn oavsett, eftersom den datan inte finns.
/// </para>
///
/// <h3>Inget barn passerar</h3>
///
/// <para>
/// Ett svar är en status och ett antal (§KM.1, beslut 2026-09-10). Audit-raden bär att en
/// kallelse öppnades och av vem — aldrig ett namn (§KM.10). Svaren själva audit-loggas inte:
/// de är många, de ändras ofta, och de står inte på §KM.10:s lista över känsliga åtgärder.
/// </para>
/// </summary>
public sealed class AttendanceService(IAttendanceCallRepository calls, IAuditLog audit)
{
    /// <summary>Öppnar kallelsen för en match. Idempotent.</summary>
    public async Task<OpenCallOutcome> OpenCallAsync(
        string slug,
        Guid matchId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);

        if (!await calls.MatchBelongsToTeamAsync(matchId, slug, cancellationToken)
            .ConfigureAwait(false))
        {
            return OpenCallOutcome.MatchNotInTeam;
        }

        if (await calls.CallExistsAsync(matchId, cancellationToken).ConfigureAwait(false))
        {
            // Redan oppnad. Ja utan en andra audit-rad: att kalla till en match som redan ar
            // kallad ar inte ett fel, och raden hade beskrivit en handelse som uteblev.
            return OpenCallOutcome.AlreadyOpen;
        }

        var call = new AttendanceCall
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            OpenedByAccountId = actorAccountId,
            OpenedUtc = DateTime.UtcNow,
        };

        await calls.AddCallAsync(call, cancellationToken).ConfigureAwait(false);

        await audit.RecordAsync(
            AuditActions.AttendanceCallOpened,
            actorAccountId,
            cancellationToken,
            matchId).ConfigureAwait(false);

        await calls.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return OpenCallOutcome.Opened;
    }

    /// <summary>Sparar eller ändrar den vuxnas svar.</summary>
    public async Task<SubmitResponseOutcome> SubmitResponseAsync(
        Guid matchId,
        Guid accountId,
        AttendanceStatus status,
        int count,
        CancellationToken cancellationToken)
    {
        var kickoff = await calls.FindKickoffUtcAsync(matchId, cancellationToken)
            .ConfigureAwait(false);

        if (kickoff is null)
        {
            return SubmitResponseOutcome.MatchNotFound;
        }

        if (!await calls.CallExistsAsync(matchId, cancellationToken).ConfigureAwait(false))
        {
            return SubmitResponseOutcome.NotCalled;
        }

        if (kickoff.Value <= DateTime.UtcNow)
        {
            return SubmitResponseOutcome.Closed;
        }

        // "Kan inte" ar noll oavsett vad som skickats -- ett antal pa ett nej sager ingenting.
        var effectiveCount = status == AttendanceStatus.CantCome ? 0 : count;

        var existing = await calls.FindResponseAsync(matchId, accountId, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;

        if (existing is null)
        {
            await calls.AddResponseAsync(
                new AttendanceResponse
                {
                    Id = Guid.NewGuid(),
                    MatchId = matchId,
                    AccountId = accountId,
                    Status = status,
                    Count = effectiveCount,
                    CreatedUtc = now,
                    UpdatedUtc = now,
                },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Samma post uppdateras -- svaret dubbleras aldrig (§KM.7, ett svar per konto).
            existing.Status = status;
            existing.Count = effectiveCount;
            existing.UpdatedUtc = now;
        }

        await calls.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return SubmitResponseOutcome.Saved;
    }

    /// <summary>Läget för en vuxen: är kallelsen öppen, när är avspark, vad svarade jag.</summary>
    public async Task<AttendanceStateDto?> GetStateAsync(
        Guid matchId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var kickoff = await calls.FindKickoffUtcAsync(matchId, cancellationToken)
            .ConfigureAwait(false);

        if (kickoff is null)
        {
            return null;
        }

        var callOpen = await calls.CallExistsAsync(matchId, cancellationToken)
            .ConfigureAwait(false);

        var mine = await calls.FindResponseAsync(matchId, accountId, cancellationToken)
            .ConfigureAwait(false);

        return new AttendanceStateDto(
            callOpen,
            kickoff.Value,
            mine is null ? null : AttendanceResponseDto.For(mine));
    }
}
