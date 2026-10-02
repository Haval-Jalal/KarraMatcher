using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Attendance;
using KarraMatcher.Domain.Attendance;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Avspark-deadlinen i <see cref="AttendanceService.RespondAsync"/> (#587): den avgörs mot en
/// injicerad <see cref="TimeProvider"/>, så båda grenarna går att pröva deterministiskt — samma
/// kallelse, olika klocka. Ren UTC-jämförelse (§KM.5).
/// </summary>
public class AttendanceRespondDeadlineTests
{
    private static readonly DateTime Kickoff = new(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid EventId = Guid.NewGuid();
    private static readonly Guid ChildId = Guid.NewGuid();
    private static readonly Guid GuardianId = Guid.NewGuid();

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static AttendanceService ServiceAt(DateTimeOffset now)
    {
        // RespondAsync rör varken medlemskap, audit eller push — så de deps lämnas osatta med flit.
        return new AttendanceService(new StubRepo(), null!, null!, null!, new FixedClock(now));
    }

    [Fact]
    public async Task FoerAvspark_SvaretSparas()
    {
        var service = ServiceAt(Kickoff.AddHours(-1));

        var outcome = await service.RespondAsync(
            EventId, ChildId, GuardianId, AttendanceReply.Coming, CancellationToken.None);

        Assert.Equal(RespondOutcome.Saved, outcome);
    }

    [Fact]
    public async Task EfterAvspark_AerStaengt()
    {
        var service = ServiceAt(Kickoff.AddSeconds(1));

        var outcome = await service.RespondAsync(
            EventId, ChildId, GuardianId, AttendanceReply.Coming, CancellationToken.None);

        Assert.Equal(RespondOutcome.Closed, outcome);
    }

    /// <summary>Bara det RespondAsync faktiskt läser är meningsfullt; resten ska aldrig anropas.</summary>
    private sealed class StubRepo : IAttendanceCallRepository
    {
        public Task<DateTime?> FindKickoffUtcAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult<DateTime?>(Kickoff);

        public Task<AttendanceCall?> FindCallByEventAsync(Guid eventId, CancellationToken ct) =>
            Task.FromResult<AttendanceCall?>(new AttendanceCall
            {
                Id = Guid.NewGuid(),
                MatchId = eventId,
                OpenedByAccountId = Guid.NewGuid(),
                OpenedUtc = Kickoff.AddDays(-7),
            });

        public Task<bool> IsGuardianOfChildAsync(Guid accountId, Guid childId, CancellationToken ct) =>
            Task.FromResult(true);

        public Task<AttendanceInvitation?> FindInvitationAsync(
            Guid callId, Guid childId, CancellationToken ct) =>
            Task.FromResult<AttendanceInvitation?>(new AttendanceInvitation
            {
                Id = Guid.NewGuid(),
                CallId = callId,
                ChildId = childId,
                Reply = null,
            });

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        // Övriga medlemmar rörs inte av RespondAsync.
        public Task<EventContext?> FindEventContextAsync(Guid eventId, CancellationToken ct) => throw new NotImplementedException();
        public Task AddCallAsync(AttendanceCall attendanceCall, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlySet<Guid>> ChildIdsInTruppAsync(Guid ageGroupId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<AttendanceInvitation>> ListInvitationsAsync(Guid callId, CancellationToken ct) => throw new NotImplementedException();
        public Task AddInvitationAsync(AttendanceInvitation invitation, CancellationToken ct) => throw new NotImplementedException();
        public void RemoveInvitation(AttendanceInvitation invitation) => throw new NotImplementedException();
        public Task<int> CountComingAsync(Guid callId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<InvitationRow>> ListInvitationRowsAsync(Guid callId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<MyInvitationRow>> ListMineAsync(Guid eventId, Guid accountId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<MyCupChildRow>> MyCupChildrenAsync(Guid ageGroupId, Guid accountId, Guid callId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<TruppCupRow>> ListTruppCupsAsync(Guid ageGroupId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<Guid>> GuardianAccountIdsForChildrenAsync(IReadOnlyCollection<Guid> childIds, CancellationToken ct) => throw new NotImplementedException();
    }
}
