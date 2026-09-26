using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Teams;

namespace KarraMatcher.Application.Features.Home;

/// <summary>
/// Bygger Hem-vyns sammanställning. Allt scopas till kontots egna medlemskap: hens trupper och
/// de lag hen ser (samma uppsättning som schemat och chatten), hens egna barns kallelser, och de
/// kanaler hen når. Ingen ny PII lämnar servern som inte redan visas för medlemmen (§KM.1/§KM.3).
/// </summary>
internal sealed class GetHomeSummaryQueryHandler(
    IMembershipService membership,
    IHomeSummaryRepository repository,
    IAccountRepository accounts,
    TimeProvider clock) : IQueryHandler<GetHomeSummaryQuery, HomeSummaryDto>
{
    public async Task<HomeSummaryDto> HandleAsync(
        GetHomeSummaryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var accountId = query.AccountId;
        var nowUtc = clock.GetUtcNow().UtcDateTime;

        // Medlemmens trupper och de lag hen ser. AccessibleTeamChannelsAsync ger exakt den
        // uppsättning lag hens roll/medlemskap når i varje trupp — samma som chatten använder.
        var trupper = await membership.MemberTrupperAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        var truppNames = new Dictionary<Guid, string>();
        var teams = new Dictionary<Guid, TeamChannelInfo>();

        foreach (var trupp in trupper)
        {
            truppNames[trupp.Id] = trupp.Name;

            var channels = await membership
                .AccessibleTeamChannelsAsync(accountId, trupp.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var team in channels)
            {
                teams[team.TeamId] = team;
            }
        }

        var truppIds = trupper.Select(t => t.Id).ToList();
        var teamIds = teams.Keys.ToList();

        var nextEvent = await NextEventAsync(teamIds, nowUtc, cancellationToken).ConfigureAwait(false);
        var pending = await PendingAsync(accountId, nowUtc, cancellationToken).ConfigureAwait(false);
        var latestChat = await LatestChatAsync(truppIds, teamIds, truppNames, teams, cancellationToken)
            .ConfigureAwait(false);

        return new HomeSummaryDto(nextEvent, pending, latestChat);
    }

    private async Task<HomeEventDto?> NextEventAsync(
        List<Guid> teamIds,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (teamIds.Count == 0)
        {
            return null;
        }

        var item = await repository.NextEventAsync(teamIds, nowUtc, cancellationToken)
            .ConfigureAwait(false);

        if (item?.Team is null)
        {
            return null;
        }

        // Samma plats-upplösning som schemat och händelsesidan: spelplatsens namn om det finns,
        // annars adressen (borta bär platsen i adressen).
        var dto = item.ToDto();
        var place = string.IsNullOrWhiteSpace(dto.Venue.Name) ? dto.Address : dto.Venue.Name;

        return new HomeEventDto(
            item.Id,
            item.Type.ToString(),
            new DateTimeOffset(item.KickoffUtc, TimeSpan.Zero),
            item.Title,
            item.OpponentName,
            item.IsHome,
            item.Team.Slug,
            item.Team.Name,
            place);
    }

    private async Task<IReadOnlyList<HomePendingKallelseDto>> PendingAsync(
        Guid accountId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var rows = await repository.PendingKallelserAsync(accountId, nowUtc, cancellationToken)
            .ConfigureAwait(false);

        return rows
            .Select(r => new HomePendingKallelseDto(
                r.EventId,
                r.Type.ToString(),
                new DateTimeOffset(r.KickoffUtc, TimeSpan.Zero),
                r.Title,
                r.Opponent,
                r.IsHome,
                r.TeamName,
                r.UnansweredCount))
            .ToList();
    }

    private async Task<HomeChatDto?> LatestChatAsync(
        List<Guid> truppIds,
        List<Guid> teamIds,
        Dictionary<Guid, string> truppNames,
        Dictionary<Guid, TeamChannelInfo> teams,
        CancellationToken cancellationToken)
    {
        if (truppIds.Count == 0)
        {
            return null;
        }

        var row = await repository.LatestChatAsync(truppIds, teamIds, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var names = await accounts
            .DisplayNamesAsync(new[] { row.AuthorAccountId }, cancellationToken)
            .ConfigureAwait(false);

        var authorName = names.TryGetValue(row.AuthorAccountId, out var name) ? name : "En medlem";

        string channelName;
        string? teamSlug = null;

        if (row.TeamId is null)
        {
            // Primärkanalen, namngiven som i kanallistan: "{trupp} chatt" (`#298`).
            channelName = $"{(truppNames.TryGetValue(row.AgeGroupId, out var truppName) ? truppName : "Truppen")} chatt";
        }
        else if (teams.TryGetValue(row.TeamId.Value, out var team))
        {
            channelName = $"Lag {team.Name} chatt";
            teamSlug = team.Slug;
        }
        else
        {
            channelName = "Lag-chatt";
        }

        return new HomeChatDto(
            row.AgeGroupId,
            teamSlug,
            channelName,
            authorName,
            Snippet(row.Body),
            new DateTimeOffset(row.PublishAtUtc, TimeSpan.Zero));
    }

    /// <summary>Ett kort utdrag ur meddelandet. Fritexten loggas aldrig (§KM.10).</summary>
    private static string Snippet(string body) =>
        body.Length <= 80 ? body : string.Concat(body.AsSpan(0, 79).TrimEnd(), "…");
}
