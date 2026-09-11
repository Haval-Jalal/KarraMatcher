using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Attendance;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class AttendanceCallRepository(KarraMatcherDbContext context)
    : IAttendanceCallRepository
{
    public async Task<bool> MatchBelongsToTeamAsync(
        Guid matchId,
        string slug,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(slug);

        return await context.Matches
            .AsNoTracking()
            .AnyAsync(m => m.Id == matchId && m.Team!.Slug == slug, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> CallExistsAsync(Guid matchId, CancellationToken cancellationToken) =>
        context.AttendanceCalls
            .AsNoTracking()
            .AnyAsync(c => c.MatchId == matchId, cancellationToken);

    public async Task AddCallAsync(AttendanceCall attendanceCall, CancellationToken cancellationToken) =>
        await context.AttendanceCalls.AddAsync(attendanceCall, cancellationToken).ConfigureAwait(false);

    public async Task<DateTime?> FindKickoffUtcAsync(
        Guid matchId,
        CancellationToken cancellationToken) =>
        await context.Matches
            .AsNoTracking()
            .Where(m => m.Id == matchId)
            .Select(m => (DateTime?)m.KickoffUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task<AttendanceResponse?> FindResponseAsync(
        Guid matchId,
        Guid accountId,
        CancellationToken cancellationToken) =>
        // Spårat, inte AsNoTracking: svaret ska gå att ändra i samma enhet av arbete.
        context.AttendanceResponses
            .FirstOrDefaultAsync(
                r => r.MatchId == matchId && r.AccountId == accountId,
                cancellationToken);

    public async Task AddResponseAsync(
        AttendanceResponse response,
        CancellationToken cancellationToken) =>
        await context.AttendanceResponses.AddAsync(response, cancellationToken).ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
