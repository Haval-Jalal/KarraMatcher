using KarraMatcher.Application.Abstractions.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Samlar allt servern har om ett konto för registerutdraget (`#67`).
///
/// <para>
/// Joinsen mot match och lag görs här, så att utdraget kan säga "match mot X" i stället för
/// ett GUID — kravet är att det ska vara läsbart för en människa. Push-prenumerationerna
/// projiceras <em>utan</em> endpoint och nycklar: secreten väljs aldrig ut ur databasen och
/// kan därför inte råka följa med (§KM.10).
/// </para>
/// </summary>
internal sealed class AccountExportRepository(KarraMatcherDbContext context)
    : IAccountExportRepository
{
    public async Task<AccountExportData?> LoadForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await context.Accounts
            .AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => new AccountExportRow(
                a.Email,
                a.FirstName,
                a.LastName,
                a.CreatedUtc,
                a.LastSignedInUtc))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return null;
        }

        var offers = await (
            from o in context.CarpoolOffers.AsNoTracking()
            where o.DriverAccountId == accountId
            join m in context.Matches on o.MatchId equals m.Id
            orderby m.KickoffUtc
            select new CarpoolOfferExportRow(
                m.OpponentName,
                m.KickoffUtc,
                o.Direction,
                o.DeparturePlace,
                o.DepartureUtc,
                o.Seats,
                o.Note,
                o.Status,
                o.CreatedUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var requests = await (
            from r in context.CarpoolRequests.AsNoTracking()
            where r.RequesterAccountId == accountId
            join o in context.CarpoolOffers on r.OfferId equals o.Id
            join m in context.Matches on o.MatchId equals m.Id
            orderby m.KickoffUtc
            select new CarpoolRequestExportRow(
                m.OpponentName,
                m.KickoffUtc,
                r.Seats,
                r.Message,
                r.ResponseMessage,
                r.Status,
                r.CreatedUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var attendance = await (
            from a in context.AttendanceResponses.AsNoTracking()
            where a.AccountId == accountId
            join m in context.Matches on a.MatchId equals m.Id
            orderby m.KickoffUtc
            select new AttendanceResponseExportRow(
                m.OpponentName,
                m.KickoffUtc,
                a.Status,
                a.Count,
                a.CreatedUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var preferences = await (
            from p in context.NotificationPreferences.AsNoTracking()
            where p.AccountId == accountId
            join t in context.Teams on p.TeamId equals t.Id
            orderby t.Name
            select new NotificationPreferenceExportRow(
                t.Name,
                p.MatchChanges,
                p.Carpool,
                p.Reminders,
                p.UpdatedUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Aldrig s.Endpoint / s.P256dh / s.Auth i select:en. Secreten lamnar inte databasen.
        var subscriptions = await (
            from s in context.PushSubscriptions.AsNoTracking()
            where s.AccountId == accountId
            join t in context.Teams on s.TeamId equals t.Id
            orderby t.Name
            select new PushSubscriptionExportRow(t.Name, s.CreatedUtc, s.LastUsedUtc))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AccountExportData(
            account,
            offers,
            requests,
            attendance,
            preferences,
            subscriptions);
    }
}
