using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Application.Features.Teams;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;

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
    IGeocoder geocoder,
    IAuditLog audit,
    IPushOutbox push)
{
    /// <summary>
    /// Lägger upp en ny händelse i laget som adressen pekar ut (`#307`). Hemma → klubbens
    /// hemmaplan; borta/annan plats → den skrivna adressen, geokodad till koordinater.
    /// </summary>
    public async Task<EventSaveResult> CreateAsync(
        string teamSlug,
        EventDraft draft,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var team = await events.FindTeamBySlugAsync(teamSlug, cancellationToken)
            .ConfigureAwait(false);

        if (team is null)
        {
            return new EventSaveResult(EventSaveOutcome.TeamNotFound, null);
        }

        var location = await ResolveLocationAsync(team, draft, cancellationToken).ConfigureAwait(false);

        if (location.Outcome != EventSaveOutcome.Ok)
        {
            return new EventSaveResult(location.Outcome, null);
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
            VenueId = location.VenueId,
            IsHome = location.IsHome,
            AddressOverride = location.Address,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
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
                item.TeamId, PushCategory.EventChange, EventNotification.Created(created)));
        }

        return new EventSaveResult(EventSaveOutcome.Ok, created);
    }

    /// <summary>Ändrar en händelse (`#307`). Hemma/borta-adressen löses om precis som vid skapande.</summary>
    public async Task<EventSaveResult> UpdateAsync(
        string teamSlug,
        Guid eventId,
        EventDraft draft,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var item = await FindInTeamAsync(teamSlug, eventId, cancellationToken).ConfigureAwait(false);

        if (item is null)
        {
            return new EventSaveResult(EventSaveOutcome.TeamNotFound, null);
        }

        var location = await ResolveLocationAsync(item.Team!, draft, cancellationToken)
            .ConfigureAwait(false);

        if (location.Outcome != EventSaveOutcome.Ok)
        {
            return new EventSaveResult(location.Outcome, null);
        }

        var before = EventSummary.Describe(item);

        // Fångas före ändringen: notisen ska kunna säga *vad* som ändrats (§KM.7-texten). Platsen
        // jämförs på den upplösta adressen (inte längre ett spelplats-id, `#307`).
        var beforeKickoffUtc = item.KickoffUtc;
        var beforeAddress = item.ToDto().Address;

        // Typen ändras inte vid redigering — en match förblir en match.
        var isMatch = item.Type == EventType.Match;

        item.KickoffUtc = draft.KickoffUtc;
        item.Title = isMatch ? null : Blank(draft.Title);
        item.OpponentName = isMatch ? draft.Opponent?.Trim() : null;
        item.VenueId = location.VenueId;
        item.IsHome = location.IsHome;
        item.AddressOverride = location.Address;
        item.Latitude = location.Latitude;
        item.Longitude = location.Longitude;
        item.Note = Blank(draft.Note);

        Touch(item);

        await audit.RecordAsync(
            AuditActions.EventUpdated,
            actorAccountId,
            cancellationToken,
            item.Id,
            EventSummary.Change(before, EventSummary.Describe(item))).ConfigureAwait(false);

        await events.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Läses om med platsen upplöst, så en "ny plats"-notis kan namnge den nya.
        var reloaded = await ReloadAsync(item.Id, cancellationToken).ConfigureAwait(false);

        if (reloaded is not null)
        {
            var message = EventNotification.Updated(
                reloaded, beforeKickoffUtc, beforeAddress, reloaded.Address);

            // Null när bara notistexten ändrats: en förälder behöver inte väckas för det.
            if (message is not null)
            {
                push.Enqueue(PushDispatch.ToTeam(item.TeamId, PushCategory.EventChange, message));
            }
        }

        return new EventSaveResult(EventSaveOutcome.Ok, reloaded);
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
            item.TeamId, PushCategory.EventChange, EventNotification.Cancelled(dto)));

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

    /// <summary>
    /// Löser händelsens plats ur utkastet (`#307`). Tre vägar, som speglar mappningen:
    /// <list type="bullet">
    /// <item>utkastet bär ett <c>VenueId</c> (import/seed) → äldre registervägen behålls;</item>
    /// <item>hemma → klubbens hemmaplan (måste vara satt); inget lagras på händelsen;</item>
    /// <item>annars → den skrivna adressen geokodas till adress + koordinat på händelsen.</item>
    /// </list>
    /// </summary>
    private async Task<Resolved> ResolveLocationAsync(
        Team team, EventDraft draft, CancellationToken cancellationToken)
    {
        if (draft.VenueId is not null)
        {
            if (!await events.VenueExistsAsync(draft.VenueId.Value, cancellationToken).ConfigureAwait(false))
            {
                return new Resolved(EventSaveOutcome.TeamNotFound, null, null, null, null, null);
            }

            return new Resolved(
                EventSaveOutcome.Ok, draft.VenueId, draft.IsHome, Blank(draft.Address), null, null);
        }

        if (draft.IsHome == true)
        {
            var club = team.AgeGroup?.Club;

            if (club?.HomeLatitude is null || club.HomeLongitude is null)
            {
                return new Resolved(EventSaveOutcome.NoHomeVenue, null, null, null, null, null);
            }

            return new Resolved(EventSaveOutcome.Ok, null, true, null, null, null);
        }

        var hits = await geocoder.LookupAsync(draft.Address?.Trim() ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        if (hits.Count == 0)
        {
            return new Resolved(EventSaveOutcome.AddressNotFound, null, null, null, null, null);
        }

        if (hits.Count > 1)
        {
            // Aldrig gissa: tränaren får skriva adressen mer exakt (samma regel som hemmaplanen).
            return new Resolved(EventSaveOutcome.AddressAmbiguous, null, null, null, null, null);
        }

        var place = hits[0];

        return new Resolved(
            EventSaveOutcome.Ok, null, false, place.Label, place.Latitude, place.Longitude);
    }

    /// <summary>Händelsens upplösta plats-fält (`#307`).</summary>
    private sealed record Resolved(
        EventSaveOutcome Outcome,
        Guid? VenueId,
        bool? IsHome,
        string? Address,
        double? Latitude,
        double? Longitude);

    /// <summary>Tom text räknas som "inget värde" — inte som ett värde som är tomt.</summary>
    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Vad ett försök att skapa/ändra en händelse slutade med (`#307`).</summary>
public enum EventSaveOutcome
{
    /// <summary>Händelsen skapades eller ändrades.</summary>
    Ok = 0,

    /// <summary>Laget (eller händelsen) finns inte, eller hör inte till adressen.</summary>
    TeamNotFound = 1,

    /// <summary>Hemma valdes men klubben har ingen hemmaplan satt — sätt den i inställningarna först.</summary>
    NoHomeVenue = 2,

    /// <summary>Bortaadressen gick inte att hitta vid geokodningen.</summary>
    AddressNotFound = 3,

    /// <summary>Bortaadressen matchade flera platser — skriv den mer exakt.</summary>
    AddressAmbiguous = 4,
}

/// <summary>Utfallet av att skapa/ändra en händelse, med DTO:n vid lyckat resultat (`#307`).</summary>
public sealed record EventSaveResult(EventSaveOutcome Outcome, EventDto? Event);

/// <summary>
/// Det en tränare fyller i om en händelse.
///
/// <para>
/// Laget finns med flit inte här — det bestäms av adressen, som är det behörigheten prövas
/// mot. Se klassdokumentationen för <see cref="EventAdminService"/>.
/// </para>
///
/// <para>
/// <see cref="Opponent"/> gäller en match, <see cref="Title"/> en träning/övrig händelse.
/// <see cref="IsHome"/> gäller alla typer (`#307`): hemma = klubbens plan, annars skriver
/// tränaren <see cref="Address"/> som geokodas. Vad som krävs bevakas i valideringen.
/// </para>
/// </summary>
public sealed record EventDraft(
    EventType Type,
    DateTime KickoffUtc,
    string? Title,
    string? Opponent,
    bool? IsHome,
    string? Address,
    string? Note,
    Guid? VenueId = null);
