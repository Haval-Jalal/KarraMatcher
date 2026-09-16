using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF-implementation av superadmins skrivåtkomst (§KM.3, `#192`). Läsvägarna kör
/// <c>AsNoTracking</c>; det som ska ändras eller kontrolleras för unikhet läses spårat.
/// </summary>
internal sealed class AdministrationRepository(KarraMatcherDbContext context) : IAdministrationRepository
{
    // ---- Sport -----------------------------------------------------------------------
    public async Task<IReadOnlyList<Sport>> GetSportsAsync(CancellationToken cancellationToken) =>
        await context.Sports.AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Sport?> FindSportAsync(Guid id, CancellationToken cancellationToken) =>
        context.Sports.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public Task<bool> SportSlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        context.Sports.AsNoTracking().AnyAsync(s => s.Slug == slug, cancellationToken);

    public async Task AddSportAsync(Sport sport, CancellationToken cancellationToken) =>
        await context.Sports.AddAsync(sport, cancellationToken).ConfigureAwait(false);

    // ---- Klubb -----------------------------------------------------------------------
    public async Task<IReadOnlyList<Club>> GetClubsAsync(CancellationToken cancellationToken) =>
        await context.Clubs.AsNoTracking()
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Club?> FindClubAsync(Guid id, CancellationToken cancellationToken) =>
        context.Clubs.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> ClubSlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        context.Clubs.AsNoTracking().AnyAsync(c => c.Slug == slug, cancellationToken);

    public Task<bool> ClubExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Clubs.AsNoTracking().AnyAsync(c => c.Id == id, cancellationToken);

    public async Task AddClubAsync(Club club, CancellationToken cancellationToken) =>
        await context.Clubs.AddAsync(club, cancellationToken).ConfigureAwait(false);

    // ---- Trupp (AgeGroup) ------------------------------------------------------------
    public async Task<IReadOnlyList<AgeGroup>> GetTrupperAsync(
        Guid? clubId, CancellationToken cancellationToken) =>
        await context.AgeGroups.AsNoTracking()
            .Include(a => a.Club)
            .Include(a => a.Sport)
            .Where(a => clubId == null || a.ClubId == clubId)
            .OrderBy(a => a.Club!.Name).ThenBy(a => a.Name).ThenBy(a => a.Season)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<AgeGroup?> FindTruppAsync(Guid id, CancellationToken cancellationToken) =>
        context.AgeGroups
            .Include(a => a.Club)
            .Include(a => a.Sport)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<bool> TruppExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.AgeGroups.AsNoTracking().AnyAsync(a => a.Id == id, cancellationToken);

    public Task<bool> SportExistsAsync(Guid id, CancellationToken cancellationToken) =>
        context.Sports.AsNoTracking().AnyAsync(s => s.Id == id, cancellationToken);

    public Task<bool> TruppNameTakenAsync(
        Guid clubId, string name, string season, Guid? excludingId, CancellationToken cancellationToken) =>
        context.AgeGroups.AsNoTracking().AnyAsync(
            a => a.ClubId == clubId
                && a.Name == name
                && a.Season == season
                && (excludingId == null || a.Id != excludingId),
            cancellationToken);

    public async Task AddTruppAsync(AgeGroup trupp, CancellationToken cancellationToken) =>
        await context.AgeGroups.AddAsync(trupp, cancellationToken).ConfigureAwait(false);

    // ---- Lag (Team) ------------------------------------------------------------------
    public async Task<IReadOnlyList<Team>> GetLagAsync(
        Guid truppId, CancellationToken cancellationToken) =>
        await context.Teams.AsNoTracking()
            .Where(t => t.AgeGroupId == truppId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<Team?> FindLagAsync(Guid id, CancellationToken cancellationToken) =>
        context.Teams.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> LagSlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        context.Teams.AsNoTracking().AnyAsync(t => t.Slug == slug, cancellationToken);

    public async Task AddLagAsync(Team lag, CancellationToken cancellationToken) =>
        await context.Teams.AddAsync(lag, cancellationToken).ConfigureAwait(false);

    // ---- Admin-tilldelning -----------------------------------------------------------
    public Task<Account?> FindAccountByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);

        var normalized = email.Trim().ToLowerInvariant();

        return context.Accounts.FirstOrDefaultAsync(a => a.Email == normalized, cancellationToken);
    }

    public Task<TeamRole?> FindAdminRoleAsync(
        Guid accountId, Guid truppId, CancellationToken cancellationToken) =>
        context.TeamRoles.FirstOrDefaultAsync(
            r => r.AccountId == accountId
                && r.AgeGroupId == truppId
                && r.Role == RoleKind.Admin,
            cancellationToken);

    public async Task<IReadOnlyList<TeamRole>> GetAdminsAsync(
        Guid truppId, CancellationToken cancellationToken) =>
        await context.TeamRoles.AsNoTracking()
            .Include(r => r.Account)
            .Where(r => r.AgeGroupId == truppId && r.Role == RoleKind.Admin)
            .OrderBy(r => r.GrantedUtc)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task AddRoleAsync(TeamRole role, CancellationToken cancellationToken) =>
        await context.TeamRoles.AddAsync(role, cancellationToken).ConfigureAwait(false);

    public void RemoveRole(TeamRole role) => context.TeamRoles.Remove(role);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
