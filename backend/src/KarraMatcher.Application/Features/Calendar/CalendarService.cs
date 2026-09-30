using System.Security.Cryptography;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Teams;
using KarraMatcher.Domain.Calendar;
using KarraMatcher.Domain.Events;

using Microsoft.Extensions.Options;

namespace KarraMatcher.Application.Features.Calendar;

/// <summary>
/// Kalender bakom medlemskap (§KM.4, återinförd som privat feed). Skapar och återkallar kontots
/// nyckel, och bygger feeden för en nyckel.
///
/// <para>
/// Feeden scopas till kontots egna lag — samma uppsättning som schemat och chatten — och bär bara
/// händelser (tid, motståndare/rubrik, plats), aldrig barn-PII (§KM.1). Är nyckeln okänd får
/// anroparen inget att gå på (feeden är då null → 404).
/// </para>
/// </summary>
public sealed class CalendarService(
    ICalendarRepository repository,
    IMembershipService membership,
    TimeProvider clock,
    IOptions<AuthOptions> options)
{
    private const int MatchDurationHours = 2;
    private const int FeedHistoryDays = 30;

    /// <summary>Kontots kalender-URL, skapar en nyckel vid första anropet.</summary>
    public async Task<string> GetLinkAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var token = await repository.FindByAccountAsync(accountId, cancellationToken).ConfigureAwait(false)
            ?? await CreateAsync(accountId, cancellationToken).ConfigureAwait(false);

        return UrlFor(token.Token);
    }

    /// <summary>Byter ut kontots nyckel — den gamla URL:en slutar fungera direkt.</summary>
    public async Task<string> RegenerateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var existing = await repository.FindByAccountAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            repository.Remove(existing);
        }

        var token = await CreateAsync(accountId, cancellationToken).ConfigureAwait(false);

        return UrlFor(token.Token);
    }

    /// <summary>Bygger feeden för en nyckel, eller null om nyckeln är okänd/återkallad.</summary>
    public async Task<string?> BuildFeedAsync(string token, CancellationToken cancellationToken)
    {
        var accountId = await repository.ResolveAccountAsync(token, cancellationToken)
            .ConfigureAwait(false);

        if (accountId is null)
        {
            return null;
        }

        var (teamIds, truppIds) = await MemberScopeAsync(accountId.Value, cancellationToken)
            .ConfigureAwait(false);
        var fromUtc = clock.GetUtcNow().UtcDateTime.AddDays(-FeedHistoryDays);

        var events = teamIds.Count == 0 && truppIds.Count == 0
            ? []
            : await repository.EventsForTeamsAsync(teamIds, truppIds, fromUtc, cancellationToken)
                .ConfigureAwait(false);

        var stamp = new DateTimeOffset(clock.GetUtcNow().UtcDateTime, TimeSpan.Zero);

        // Trupp-vida händelser (Team == null) tas nu med (#475) — etiketten härleds ur truppnamnet
        // i ToEntry i stället för att raden filtreras bort.
        var entries = events.Select(item => ToEntry(item, stamp)).ToList();

        return CalendarBuilder.Build("Kärra Matcher", entries);
    }

    private async Task<CalendarToken> CreateAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var token = new CalendarToken
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Token = NewToken(),
            CreatedUtc = clock.GetUtcNow().UtcDateTime,
        };

        await repository.AddAsync(token, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return token;
    }

    private static CalendarEventEntry ToEntry(Event item, DateTimeOffset stamp)
    {
        // Samma plats-upplösning som schemat: spelplatsens namn om det finns, annars adressen.
        var dto = item.ToDto();
        var place = string.IsNullOrWhiteSpace(dto.Venue.Name) ? dto.Address : dto.Venue.Name;

        var start = new DateTimeOffset(item.KickoffUtc, TimeSpan.Zero);

        // Etiketten bärs av lagets namn, eller truppens för en trupp-övergripande händelse utan
        // lag (#475) — samma ordning som Hem-vyn.
        var groupLabel = item.Team?.Name ?? item.AgeGroup?.Name ?? "Truppen";

        return new CalendarEventEntry(
            Uid: $"{item.Id:N}@karramatcher",
            StartUtc: start,
            EndUtc: start.AddHours(MatchDurationHours),
            Summary: $"{groupLabel} – {Label(item)}",
            Location: place,
            Cancelled: item.Status == EventStatus.Cancelled,
            Sequence: item.IcsSequence,
            StampUtc: stamp,
            // Adminens/tränarens notis till föräldrarna (`#468`) som DESCRIPTION. Feeden är privat
            // per medlem och återkallningsbar (§KM.4), så mottagarkretsen är densamma som i appen.
            Description: dto.Note ?? string.Empty);
    }

    private static string Label(Event item) => item.Type switch
    {
        EventType.Match => $"{(item.IsHome == true ? "Hemma" : "Borta")} mot {item.OpponentName}".Trim(),
        _ => item.Title ?? item.Type.ToString(),
    };

    private async Task<(List<Guid> TeamIds, List<Guid> TruppIds)> MemberScopeAsync(
        Guid accountId, CancellationToken cancellationToken)
    {
        // De trupper och lag kontot ser — samma uppsättning som Hem-vyn och chatten bygger på.
        // Trupp-id:na behövs för trupp-övergripande händelser (utan lag, #475).
        var trupper = await membership.MemberTrupperAsync(accountId, cancellationToken)
            .ConfigureAwait(false);

        var teamIds = new HashSet<Guid>();

        foreach (var trupp in trupper)
        {
            var channels = await membership
                .AccessibleTeamChannelsAsync(accountId, trupp.Id, cancellationToken)
                .ConfigureAwait(false);

            foreach (var team in channels)
            {
                teamIds.Add(team.TeamId);
            }
        }

        return ([.. teamIds], trupper.Select(trupp => trupp.Id).ToList());
    }

    private string UrlFor(string token) =>
        $"{options.Value.AppBaseUrl.TrimEnd('/')}/api/v1/kalender/{token}.ics";

    /// <summary>256 bitar, URL-säkert. Ogissbart, precis som en session-token (SessionIssuer).</summary>
    private static string NewToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
