using KarraMatcher.Domain.Cup;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>Ett barn placerat i ett cup-lag, med namn för visning (`#335`). Visas som "Liam J" (§KM.1).</summary>
public sealed record CupTeamMemberRow(Guid CupTeamId, Guid ChildId, string FirstName, string LastInitial);

/// <summary>
/// Läser och skriver cup-lagen (`#335`): de tillfälliga lag en admin bygger av de anmälda barnen.
/// Skilt från de permanenta färg-lagens repository — cup-lagen är en egen, kortlivad sak.
/// </summary>
public interface ICupTeamRepository
{
    /// <summary>Cup-lagen för en cup, i skapandeordning.</summary>
    public Task<IReadOnlyList<CupTeam>> ListTeamsAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>De placerade barnen (med namn) i cupens lag.</summary>
    public Task<IReadOnlyList<CupTeamMemberRow>> ListMembersAsync(
        Guid eventId, CancellationToken cancellationToken);

    /// <summary>Ett cup-lag, spårat. Null när det inte finns.</summary>
    public Task<CupTeam?> FindTeamAsync(Guid cupTeamId, CancellationToken cancellationToken);

    public Task AddTeamAsync(CupTeam team, CancellationToken cancellationToken);

    public void RemoveTeam(CupTeam team);

    /// <summary>
    /// Barnets placering i <em>något</em> av cupens lag, spårad — så en ny placering flyttar
    /// barnet i stället för att låta det stå i två lag. Null när barnet inte är placerat.
    /// </summary>
    public Task<CupTeamMember?> FindMemberByChildAsync(
        Guid eventId, Guid childId, CancellationToken cancellationToken);

    public Task AddMemberAsync(CupTeamMember member, CancellationToken cancellationToken);

    public void RemoveMember(CupTeamMember member);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
