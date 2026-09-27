using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Cup;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class CupTeamRepository(KarraMatcherDbContext context) : ICupTeamRepository
{
    public async Task<IReadOnlyList<CupTeam>> ListTeamsAsync(
        Guid eventId, CancellationToken cancellationToken) =>
        await context.CupTeams
            .AsNoTracking()
            .Where(team => team.EventId == eventId)
            .OrderBy(team => team.CreatedUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<CupTeamMemberRow>> ListMembersAsync(
        Guid eventId, CancellationToken cancellationToken) =>
        await (
            from member in context.CupTeamMembers.AsNoTracking()
            join team in context.CupTeams.AsNoTracking() on member.CupTeamId equals team.Id
            join child in context.Children.AsNoTracking() on member.ChildId equals child.Id
            where team.EventId == eventId
            select new CupTeamMemberRow(member.CupTeamId, member.ChildId, child.FirstName, child.LastInitial))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<CupTeam?> FindTeamAsync(Guid cupTeamId, CancellationToken cancellationToken) =>
        await context.CupTeams
            .FirstOrDefaultAsync(team => team.Id == cupTeamId, cancellationToken)
            .ConfigureAwait(false);

    public async Task AddTeamAsync(CupTeam team, CancellationToken cancellationToken) =>
        await context.CupTeams.AddAsync(team, cancellationToken).ConfigureAwait(false);

    public void RemoveTeam(CupTeam team) => context.CupTeams.Remove(team);

    public async Task<CupTeamMember?> FindMemberByChildAsync(
        Guid eventId, Guid childId, CancellationToken cancellationToken) =>
        await (
            from member in context.CupTeamMembers
            join team in context.CupTeams on member.CupTeamId equals team.Id
            where team.EventId == eventId && member.ChildId == childId
            select member)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddMemberAsync(CupTeamMember member, CancellationToken cancellationToken) =>
        await context.CupTeamMembers.AddAsync(member, cancellationToken).ConfigureAwait(false);

    public void RemoveMember(CupTeamMember member) => context.CupTeamMembers.Remove(member);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
