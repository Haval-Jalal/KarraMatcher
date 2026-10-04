using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Invitations;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

/// <summary>
/// Raderar döda inbjudningar (§KM.6, `#585`).
///
/// <para>
/// Bara id:n läses hem — adressen hämtas aldrig till minnet (§KM.10), en gallring som läser
/// in det den ska radera har missförstått sin uppgift. Raderingen sker genom att fästa tomma
/// stubbar med rätt id. Inget pekar på en inbjudan, så ingen FK-ordning behöver hållas.
/// </para>
/// </summary>
internal sealed class InvitationRetentionRepository(KarraMatcherDbContext context)
    : IInvitationRetentionRepository
{
    public async Task<int> PurgeDeadOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken)
    {
        // Allt utom accepterade: en accepterad inbjudan är ett medlemskap och kaskaderar bort
        // med sitt konto. En utgången väntande eller en återkallad är död och tas bort här.
        var deadIds = await context.Invitations
            .AsNoTracking()
            .Where(i => i.Status != InvitationStatus.Accepted && i.ExpiresUtc < cutoffUtc)
            .Select(i => i.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (deadIds.Count == 0)
        {
            return 0;
        }

        context.Invitations.RemoveRange(
            deadIds.Select(id => new Invitation { Id = id, Email = string.Empty, TokenHash = string.Empty }));

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return deadIds.Count;
    }
}
