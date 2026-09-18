using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Application.Features.Teams;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Events.Admin;

/// <summary>
/// Tränarens händelsehantering: skapa, ändra, ställa in och ta bort matcher, träningar och
/// övriga händelser (`#198`).
///
/// <para>
/// Samlat på ett ställe eftersom tre regler måste gälla för <em>varje</em> ändring, och
/// ingen av dem är självklar att komma ihåg en i taget.
/// </para>
///
/// <para>
/// <b>1. Kalenderprenumerationerna måste få veta.</b> En ändrad händelse som inte ökar
/// <c>SEQUENCE</c> uppdateras inte i föräldrarnas kalendrar (§KM.4).
/// </para>
///
/// <para>
/// <b>2. Åtgärden ska gå att spåra.</b> "Vem flyttade händelsen?" ska gå att besvara utan
/// att gissa (§KM.10). Före- och eftervärde loggas — men aldrig notisen, som är fritext.
/// </para>
///
/// <para>
/// <b>3. Laget bestäms av adressen, inte av indata.</b> Behörigheten prövas mot lagets slug
/// i routen (policyn <c>CoachOfTeam</c>). Toge kommandot emot ett lag-id i kroppen kunde en
/// tränare för Gul skicka Blås id och kringgå kontrollen helt.
/// </para>
/// </summary>
public sealed class EventAdminService(
    IEventAdminRepository events,
    IAuditLog audit,
    IPushOutbox push)
{
    /// <summary>Lägger upp en ny händelse i laget som adressen pekar ut.</summary>
    public async Task<EventDto?> CreateAsync(
        string teamSlug,
        EventDraft draft,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var team = await events.FindTeamBySlugAsync(teamSlug, cancellationToken)
            .ConfigureAwait(false);

        if (team is null || !await events.VenueExistsAsync(draft.VenueId, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        var isMatch = draft.Type == EventType.Match;

        var item = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            Type = draft.Type,
            KickoffUtc = draft.KickoffUtc,
            Title = isMatch ? null : Blank(draft.Title),
            OpponentName = isMatch ? draft.Opponent?.Trim() : null,
            VenueId = draft.VenueId,
            IsHome = isMatch ? draft.IsHome : null,
            AddressOverride = Blank(draft.AddressOverride),
            Note = Blank(draft.Note),
            Status = EventStatus.Scheduled,
            IcsSequence = 0,
            UpdatedUtc = DateTime.UtcNow,
        };

        await events.AddAsync(item, cancellationToken).ConfigureAwait(false);

        await audit.RecordAsync(
            AuditActions.EventCreated,
            actorAccountId,
            cancellationToken,
            item.Id,
            EventSummary.Describe(item)).ConfigureAwait(false);

        await events.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var created = await ReloadAsync(item.Id, cancellationToken).ConfigureAwait(false);

        // Notisen köas efter att händelsen sparats, aldrig i requesten (§KM.11) -- tränaren
        // får sitt svar direkt, utskicket sköts av bakgrundstjänsten.
        if (created is not null)
        {
            push.Enqueue(PushDispatch.ToTeam(
                item.TeamId, PushCategory.MatchChange, EventNotification.Created(created)));
        }

        return created;
    }

    /// <summary>Ändrar en händelse. Svarar null när den inte finns eller inte hör till laget.</summary>
    public async Task<EventDto?> UpdateAsync(
        string teamSlug,
        Guid eventId,
        EventDraft draft,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var item = await FindInTeamAsync(teamSlug, eventId, cancellationToken).ConfigureAwait(false);

        if (item is null || !await events.VenueExistsAsync(draft.VenueId, cancellationToken)
            .ConfigureAwait(false))
        {
            return null;
        }

        var before = EventSummary.Describe(item);

        // Fångas före ändringen: notisen ska kunna säga *vad* som ändrats (§KM.7-texten),
        // inte bara att något gjorde det.
        var beforeKickoffUtc = item.KickoffUtc;
        var beforeVenueId = item.VenueId;

        // Typen ändras inte vid redigering — en match förblir en match.
        var isMatch = item.Type == EventType.Match;

        item.KickoffUtc = draft.KickoffUtc;
        item.Title = isMatch ? null : Blank(draft.Title);
        item.OpponentName = isMatch ? draft.Opponent?.Trim() : null;
        item.VenueId = draft.VenueId;
        item.IsHome = isMatch ? draft.IsHome : null;
        item.AddressOverride = Blank(draft.AddressOverride);
        item.Note = Blank(draft.Note);

        Touch(item);

        await audit.RecordAsync(
            AuditActions.EventUpdated,
            actorAccountId,
            cancellationToken,
            item.Id,
            EventSummary.Change(before, EventSummary.Describe(item))).ConfigureAwait(false);

        await events.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Läses om med spelplatsen inläst, så en "ny plats"-notis kan namnge den nya.
        var reloaded = await ReloadAsync(item.Id, cancellationToken).ConfigureAwait(false);

        if (reloaded is not null)
        {
            var message = EventNotification.Updated(
                reloaded, beforeKickoffUtc, beforeVenueId, item.VenueId);

            // Null när bara notistexten ändrats: en förälder behöver inte väckas för det.
            if (message is not null)
            {
                push.Enqueue(PushDispatch.ToTeam(item.TeamId, PushCategory.MatchChange, message));
            }
        }

        return reloaded;
    }

    /// <summary>
    /// Ställer in en händelse.
    ///
    /// <para>
    /// Inställd och inte raderad: kalenderposten ska bli kvar med
    /// <c>STATUS:CANCELLED</c> (§KM.4). Försvinner posten helt står den kvar i
    /// föräldrarnas kalendrar som om ingenting hänt.
    /// </para>
    /// </summary>
    public async Task<EventDto?> CancelAsync(
        string teamSlug,
        Guid eventId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var item = await FindInTeamAsync(teamSlug, eventId, cancellationToken).ConfigureAwait(false);

        if (item is null)
        {
            return null;
        }

        var before = EventSummary.Describe(item);

        item.Status = EventStatus.Cancelled;
        Touch(item);

        await audit.RecordAsync(
            AuditActions.EventCancelled,
            actorAccountId,
            cancellationToken,
            item.Id,
            EventSummary.Change(before, EventSummary.Describe(item))).ConfigureAwait(false);

        await events.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dto = item.ToDto();

        // "Åk inte till spelplatsen" är hela poängen med den här notisen — en inställd
        // händelse som ingen får veta om är den som får någon att stå ensam på en plan.
        push.Enqueue(PushDispatch.ToTeam(
            item.TeamId, PushCategory.MatchChange, EventNotification.Cancelled(dto)));

        return dto;
    }

    /// <summary>
    /// Tar bort en händelse helt.
    ///
    /// <para>
    /// För en händelse som aldrig skulle ha lagts in. En som ställts in ska ställas in,
    /// inte raderas — se <see cref="CancelAsync"/>.
    /// </para>
    /// </summary>
    public async Task<bool> DeleteAsync(
        string teamSlug,
        Guid eventId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var item = await FindInTeamAsync(teamSlug, eventId, cancellationToken).ConfigureAwait(false);

        if (item is null)
        {
            return false;
        }

        await audit.RecordAsync(
            AuditActions.EventDeleted,
            actorAccountId,
            cancellationToken,
            item.Id,
            EventSummary.Describe(item)).ConfigureAwait(false);

        events.Remove(item);

        await events.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Läser om händelsen med spelplatsen inläst, så svaret får med adress och koordinater.
    /// </summary>
    private async Task<EventDto?> ReloadAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var saved = await events.FindForUpdateAsync(eventId, cancellationToken).ConfigureAwait(false);

        return saved?.ToDto();
    }

    /// <summary>
    /// Händelsen, men bara om den hör till laget i adressen.
    ///
    /// <para>
    /// Objektnivå-auktorisering (checklistan 2.6). Policyn har redan slagit fast att
    /// anroparen är tränare för <em>laget</em> — den här kontrollen slår fast att
    /// <em>händelsen</em> hör dit. Utan den kunde en tränare för Gul ändra en händelse i Blå
    /// genom att skicka Blås id till sin egen lagadress.
    /// </para>
    /// </summary>
    private async Task<Event?> FindInTeamAsync(
        string teamSlug,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var item = await events.FindForUpdateAsync(eventId, cancellationToken).ConfigureAwait(false);

        return item?.Team?.Slug == teamSlug ? item : null;
    }

    /// <summary>
    /// Märker händelsen som ändrad.
    ///
    /// <para>
    /// <c>IcsSequence</c> ökas vid varje ändring. Utan det uppdateras inte föräldrarnas
    /// kalendrar (§KM.4), och appen visar en tid telefonen inte känner till.
    /// </para>
    /// </summary>
    private static void Touch(Event item)
    {
        item.IcsSequence++;
        item.UpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>Tom text räknas som "inget värde" — inte som ett värde som är tomt.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Det en tränare fyller i om en händelse.
///
/// <para>
/// Laget finns med flit inte här — det bestäms av adressen, som är det behörigheten prövas
/// mot. Se klassdokumentationen för <see cref="EventAdminService"/>.
/// </para>
///
/// <para>
/// <see cref="Opponent"/> och <see cref="IsHome"/> gäller en match; <see cref="Title"/> en
/// träning eller övrig händelse. Vad som krävs för vilken typ bevakas i valideringen.
/// </para>
/// </summary>
public sealed record EventDraft(
    EventType Type,
    DateTime KickoffUtc,
    string? Title,
    string? Opponent,
    Guid VenueId,
    bool? IsHome,
    string? AddressOverride,
    string? Note);
