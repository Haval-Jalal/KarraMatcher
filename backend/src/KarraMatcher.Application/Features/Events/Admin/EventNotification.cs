using System.Globalization;

using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Common;

namespace KarraMatcher.Application.Features.Events.Admin;

/// <summary>
/// Notistexten för en händelseändring (`#62`, `#198`, §KM.1, §KM.2).
///
/// <h3>Säger vad som ändrats, inte bara att något ändrats</h3>
///
/// <para>
/// En notis som bara säger "händelsen har ändrats" tvingar alla att öppna appen för att se
/// om det rör dem. En som säger "Ny tid: lördag 14:00" gör jobbet redan på låsskärmen.
/// Därför skiljer den här på en flyttad händelse (ny tid) och en ändrad (ny plats).
/// </para>
///
/// <h3>Ingenting känsligt i texten</h3>
///
/// <para>
/// Notisen visas på en låsskärm som vem som helst i rummet kan se. Den bär bara sådant som
/// ändå står i schemat: matchens motståndare/hemma-borta eller händelsens rubrik, tid och
/// plats. <b>Aldrig tränarens notis</b> (fritext, potentiell PII, §KM.1), aldrig något om
/// ett barn (§KM.1) eller spelarkortet (§KM.2).
/// </para>
///
/// <h3>Tid i svensk tid, på ett ställe</h3>
///
/// <para>
/// Databasen lagrar UTC; en förälder läser svensk tid (§KM.5). Omräkningen sker genom
/// <see cref="SwedishTime"/>, samma väg som kalendern.
/// </para>
/// </summary>
internal static class EventNotification
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>En ny händelse har lagts upp.</summary>
    public static PushMessage Created(EventDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new PushMessage(
            $"Ny {Kind(item.Type)} {Label(item)}",
            $"{When(item.KickoffUtc)} · {Place(item)}",
            Url(item.Id));
    }

    /// <summary>En händelse har ställts in.</summary>
    public static PushMessage Cancelled(EventDto item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new PushMessage(
            $"Inställt: {Kind(item.Type)} {Label(item)}",
            "Händelsen äger inte rum. Åk inte till spelplatsen.",
            Url(item.Id));
    }

    /// <summary>
    /// En händelse har ändrats. Returnerar null när ingenting en förälder behöver rusa efter
    /// har ändrats — bytt tid eller plats ger en notis, en ändrad notistext gör det inte.
    /// </summary>
    public static PushMessage? Updated(
        EventDto item, DateTime beforeKickoffUtc, string beforeAddress, string afterAddress)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.KickoffUtc.UtcDateTime != beforeKickoffUtc)
        {
            return new PushMessage(
                $"Ny tid {Label(item)}",
                When(item.KickoffUtc),
                Url(item.Id));
        }

        if (!string.Equals(afterAddress, beforeAddress, StringComparison.Ordinal))
        {
            return new PushMessage(
                $"Ny plats {Label(item)}",
                Place(item),
                Url(item.Id));
        }

        return null;
    }

    /// <summary>Kvällspåminnelsen om morgondagens händelse (`#64`).</summary>
    public static PushMessage Reminder(DueEvent due)
    {
        ArgumentNullException.ThrowIfNull(due);

        return new PushMessage(
            $"{Kind(due.Type)} i morgon {EventDisplay.Label(due.Type, due.IsHome, due.Opponent, due.Title)}"
                .TrimEnd(),
            $"{When(due.KickoffUtc)} · {due.VenueName}",
            Url(due.EventId));
    }

    private static string Label(EventDto item) =>
        EventDisplay.Label(item.Type, item.IsHome, item.Opponent, item.Title);

    // Platsens namn på låsskärmen: spelplatsens namn (hemma) om det finns, annars adressen
    // (borta/annan plats har inget namn, bara en adress) (`#307`).
    private static string Place(EventDto item) =>
        string.IsNullOrWhiteSpace(item.Venue.Name) ? item.Address : item.Venue.Name;

    // Ordet för typen, gement, som glider in i en mening ("Ny match …", "Inställt: träning …").
    private static string Kind(string type) => type switch
    {
        "Match" => "match",
        "Training" => "träning",
        _ => "händelse",
    };

    private static string When(DateTimeOffset kickoffUtc) => When(kickoffUtc.UtcDateTime);

    private static string When(DateTime kickoffUtc)
    {
        var local = SwedishTime.ToSwedish(kickoffUtc);

        // T.ex. "lördag 8 oktober kl. 14:00".
        return local.ToString("dddd d MMMM 'kl.' HH:mm", Swedish);
    }

    private static string Url(Guid eventId) => $"/handelse/{eventId}";
}
