using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Chat;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence.Repositories;

internal sealed class ChatRepository(KarraMatcherDbContext context) : IChatRepository
{
    public Task<bool> TruppExistsAsync(Guid ageGroupId, CancellationToken cancellationToken) =>
        context.AgeGroups.AsNoTracking().AnyAsync(a => a.Id == ageGroupId, cancellationToken);

    public async Task<TeamChannel?> FindTeamChannelAsync(
        string slug, CancellationToken cancellationToken) =>
        await context.Teams
            .AsNoTracking()
            .Where(t => t.Slug == slug)
            .Select(t => new TeamChannel(t.Id, t.AgeGroupId))
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task AddMessageAsync(ChatMessage message, CancellationToken cancellationToken) =>
        await context.ChatMessages.AddAsync(message, cancellationToken).ConfigureAwait(false);

    public Task<ChatMessage?> FindMessageAsync(Guid id, CancellationToken cancellationToken) =>
        context.ChatMessages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ChatMessage>> ListPublishedAsync(
        Guid ageGroupId, Guid? teamId, int limit, CancellationToken cancellationToken)
    {
        var query = InChannel(context.ChatMessages.AsNoTracking(), ageGroupId, teamId)
            .Where(m => m.PublishedUtc != null);

        // Senaste N, sedan vänt till äldst-först — chattens läsordning.
        var latest = await query
            .OrderByDescending(m => m.PublishAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        latest.Reverse();
        return latest;
    }

    public async Task<IReadOnlyList<ChatMessage>> ListScheduledForAuthorAsync(
        Guid ageGroupId, Guid? teamId, Guid accountId, CancellationToken cancellationToken) =>
        await InChannel(context.ChatMessages.AsNoTracking(), ageGroupId, teamId)
            .Where(m => m.PublishedUtc == null
                && m.DeletedUtc == null
                && m.AuthorAccountId == accountId)
            .OrderBy(m => m.PublishAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<ChatMessage>> ListDueForReleaseAsync(
        DateTime nowUtc, CancellationToken cancellationToken) =>
        await context.ChatMessages
            .Where(m => m.PublishedUtc == null && m.DeletedUtc == null && m.PublishAtUtc <= nowUtc)
            .OrderBy(m => m.PublishAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void RemoveMessage(ChatMessage message) => context.ChatMessages.Remove(message);

    public Task<bool> ReportExistsAsync(
        Guid messageId, Guid accountId, CancellationToken cancellationToken) =>
        context.ChatReports
            .AsNoTracking()
            .AnyAsync(r => r.MessageId == messageId && r.ReportedByAccountId == accountId, cancellationToken);

    public async Task AddReportAsync(ChatReport report, CancellationToken cancellationToken) =>
        await context.ChatReports.AddAsync(report, cancellationToken).ConfigureAwait(false);

    public Task<int> ReportCountAsync(Guid messageId, CancellationToken cancellationToken) =>
        context.ChatReports.AsNoTracking().CountAsync(r => r.MessageId == messageId, cancellationToken);

    public async Task<IReadOnlyList<ReportedMessageRow>> ListReportedForTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken)
    {
        // Hela truppen: trupp-kanalen och alla dess lag-kanaler (alla rader med denna AgeGroupId).
        var messages = context.ChatMessages.AsNoTracking().Where(m => m.AgeGroupId == ageGroupId);

        // Platt join, sedan gruppering i minnet: att projicera gruppens element till en lista
        // översätts inte till en enda SQL av EF. Kön är liten (bara anmälda meddelanden).
        var flat = await (
            from r in context.ChatReports.AsNoTracking()
            join m in messages on r.MessageId equals m.Id
            select new
            {
                m.Id,
                m.AuthorAccountId,
                m.Body,
                m.PublishedUtc,
                m.DeletedUtc,
                r.Reason,
                r.CreatedUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. flat
                .GroupBy(x => new { x.Id, x.AuthorAccountId, x.Body, x.PublishedUtc, x.DeletedUtc })
                .Select(g => new ReportedMessageRow(
                    g.Key.Id,
                    g.Key.AuthorAccountId,
                    g.Key.Body,
                    g.Key.PublishedUtc,
                    g.Key.DeletedUtc != null,
                    [.. g.OrderByDescending(x => x.CreatedUtc)
                        .Select(x => new ReportReason(x.Reason, x.CreatedUtc))]))
                .OrderByDescending(row => row.Reasons.Count),
        ];
    }

    public async Task<IReadOnlyList<Guid>> ChatDisabledAccountIdsAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.NotificationPreferences
            .AsNoTracking()
            .Where(p => !p.Chat
                && context.Teams.Any(t => t.Id == p.TeamId && t.AgeGroupId == ageGroupId))
            .Select(p => p.AccountId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<Guid?> AnyTeamIdAsync(Guid ageGroupId, CancellationToken cancellationToken) =>
        await context.Teams
            .AsNoTracking()
            .Where(t => t.AgeGroupId == ageGroupId)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);

    /// <summary>
    /// Filtrerar till en kanal. Nullbart <paramref name="teamId"/> hanteras uttryckligen:
    /// en parametriserad <c>== null</c>-jämförelse mot Postgres blir annars alltid falsk.
    /// </summary>
    private static IQueryable<ChatMessage> InChannel(
        IQueryable<ChatMessage> source, Guid ageGroupId, Guid? teamId)
    {
        source = source.Where(m => m.AgeGroupId == ageGroupId);

        return teamId is null
            ? source.Where(m => m.TeamId == null)
            : source.Where(m => m.TeamId == teamId);
    }
}
