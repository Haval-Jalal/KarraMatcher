using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Attendance;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Raderar kallelser för händelser som passerat (§KM.7/§KM.10, `#203`). Samma mönster som
/// samåkningens gallring: bara id:n läses hem, och barn-raderna (svaren) tas bort före
/// föräldra-raden (kallelsen) så att främmande nyckeln inte stoppar raderingen.
/// </summary>
internal sealed class AttendanceRetentionRepository(KarraMatcherDbContext context)
    : IAttendanceRetentionRepository
{
    public async Task<int> PurgeOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        // Händelserna som passerat gränsen tas ut för sig, så båda raderingarna utgår från
        // exakt samma uppsättning.
        var expired = await context.Events
            .AsNoTracking()
            .Where(e => e.KickoffUtc < cutoffUtc)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (expired.Count == 0)
        {
            return 0;
        }

        var callIds = await context.AttendanceCalls
            .AsNoTracking()
            .Where(c => expired.Contains(c.MatchId))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (callIds.Count == 0)
        {
            return 0;
        }

        var invitationIds = await context.AttendanceInvitations
            .AsNoTracking()
            .Where(i => callIds.Contains(i.CallId))
            .Select(i => i.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Stubbar med bara nyckeln satt — innehållet läses aldrig in. Svaren (barn) före
        // kallelsen (förälder).
        context.AttendanceInvitations.RemoveRange(
            invitationIds.Select(id => new AttendanceInvitation { Id = id }));
        context.AttendanceCalls.RemoveRange(
            callIds.Select(id => new AttendanceCall { Id = id }));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return callIds.Count;
    }
}
