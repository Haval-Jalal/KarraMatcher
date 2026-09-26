using System.Diagnostics.CodeAnalysis;

using KarraMatcher.Domain.Teams;

namespace KarraMatcher.Domain.Events;

/// <summary>
/// En händelse för ett lag — en match, en träning eller något annat (`#198`).
///
/// <para>
/// Ersätter den tidigare match-only-modellen: en match är numera en händelse av typen
/// <see cref="EventType.Match"/>. Starttiden lagras alltid i UTC och visas i Europe/Stockholm
/// (§KM.5) — säsongen sträcker sig förbi sommartidsskiftet i oktober, och en händelse som visas
/// en timme fel är det som får folk att sluta lita på appen.
/// </para>
///
/// <para>
/// Matchspecifika fält (<see cref="OpponentName"/>, <see cref="IsHome"/>) är nullbara: de bär
/// bara mening för en match. En träning eller övrig händelse har i stället en
/// <see cref="Title"/>. Vad som krävs för vilken typ bevakas i valideringen, inte här.
/// </para>
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "Domänbegreppet är 'händelse' = Event (`#198`); appen har inga VB-konsumenter.")]
public sealed class Event
{
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    public Team? Team { get; set; }

    /// <summary>Vad slags händelse det är. Befintliga rader migrerades till <c>Match</c> (`#198`).</summary>
    public EventType Type { get; set; }

    /// <summary>Starttid i UTC. Kind måste vara <see cref="DateTimeKind.Utc"/>.</summary>
    public DateTime KickoffUtc { get; set; }

    /// <summary>Rubrik för en träning eller övrig händelse. Null för en match (härleds ur motståndaren).</summary>
    public string? Title { get; set; }

    /// <summary>Motståndare. Satt endast för en match.</summary>
    public string? OpponentName { get; set; }

    /// <summary>
    /// Äldre spelplatsregister-koppling (`#110`). Nullbar sedan `#307`: nya händelser pekar inte
    /// på registret utan löser platsen ur klubbens hemmaplan (hemma) eller ur händelsens egen
    /// adress + koordinat (borta/annan plats). Behålls så seedade/gamla rader fortsätter fungera.
    /// </summary>
    public Guid? VenueId { get; set; }

    public Venue? Venue { get; set; }

    /// <summary>
    /// Platsens adress när det inte är hemmaplan (`#307`): en borta-match eller en aktivitet på
    /// annan plats (t.ex. vinterträning inomhus). Tom för en hemma-händelse — då gäller klubbens.
    /// </summary>
    public string? AddressOverride { get; set; }

    /// <summary>Latitud för händelsens egen adress (borta/annan plats), geokodad (`#307`).</summary>
    public double? Latitude { get; set; }

    /// <summary>Longitud för händelsens egen adress (borta/annan plats), geokodad (`#307`).</summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Hemma på klubbens plan, eller på annan plats. Sedan `#307` meningsfull för <b>alla</b>
    /// typer: sant = klubbens hemmaplan (adress autofylls), falskt = händelsens egen adress.
    /// </summary>
    public bool? IsHome { get; set; }

    public EventStatus Status { get; set; }

    public string? Note { get; set; }

    /// <summary>
    /// Ökas vid varje ändring. Utan det uppdaterar inte föräldrarnas
    /// kalenderprenumerationer sig när en händelse flyttas (§KM.4).
    /// </summary>
    public int IcsSequence { get; set; }

    public DateTime UpdatedUtc { get; set; }

    /// <summary>
    /// När kvällspåminnelsen skickades, eller null (`#64`).
    ///
    /// <para>
    /// Markören som gör det schemalagda jobbet idempotent: en händelse påminns en gång, och en
    /// dubbelkörning ser att den redan är märkt och hoppar över den. Inget att visa för en
    /// förälder — den finns bara för jobbet.
    /// </para>
    /// </summary>
    public DateTime? ReminderSentUtc { get; set; }
}
