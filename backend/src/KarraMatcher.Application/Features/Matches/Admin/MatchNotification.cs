using System.Globalization;

using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Common;

namespace KarraMatcher.Application.Features.Matches.Admin;

/// <summary>
/// Notistexten för en matchändring (`#62`, §KM.1, §KM.2).
///
/// <h3>Säger vad som ändrats, inte bara att något ändrats</h3>
///
/// <para>
/// En notis som bara säger "matchen har ändrats" tvingar alla att öppna appen för att se om
/// det rör dem. En som säger "Ny tid: lördag 14:00" gör jobbet redan på låsskärmen. Därför
/// skiljer den här på en flyttad match (ny tid) och en ändrad (ny plats), och säger vilket.
/// </para>
///
/// <h3>Ingenting känsligt i texten</h3>
///
/// <para>
/// Notisen visas på en låsskärm som vem som helst i rummet kan se, och ligger kvar i
/// notiscentret. Den bär bara sådant som ändå står i det publika schemat: motståndare, tid,
/// plats, hemma/borta. <b>Aldrig tränarens notis</b> (fritext, potentiell PII, §KM.1),
/// aldrig något om ett barn (§KM.1) eller spelarkortet (§KM.2).
/// </para>
///
/// <h3>Tid i svensk tid, på ett ställe</h3>
///
/// <para>
/// Databasen lagrar UTC; en förälder läser svensk tid (§KM.5). Omräkningen sker genom
/// <see cref="SwedishTime"/>, samma väg som kalendern — inte med en egen uträkning som kan
/// hamna en timme fel över oktoberskiftet.
/// </para>
/// </summary>
internal static class MatchNotification
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    /// <summary>En ny match har lagts upp.</summary>
    public static PushMessage Created(MatchDto match)
    {
        ArgumentNullException.ThrowIfNull(match);

        return new PushMessage(
            $"Ny match {SideAndOpponent(match)}",
            $"{When(match.KickoffUtc)} · {match.Venue.Name}",
            Url(match.Id));
    }

    /// <summary>En match har ställts in.</summary>
    public static PushMessage Cancelled(MatchDto match)
    {
        ArgumentNullException.ThrowIfNull(match);

        return new PushMessage(
            $"Inställt: matchen {SideAndOpponent(match)}",
            "Matchen spelas inte. Åk inte till spelplatsen.",
            Url(match.Id));
    }

    /// <summary>
    /// En match har ändrats. Returnerar null när ingenting en förälder behöver rusa efter
    /// har ändrats — bytt tid eller plats ger en notis, en ändrad notistext gör det inte.
    /// </summary>
    public static PushMessage? Updated(MatchDto match, DateTime beforeKickoffUtc, Guid beforeVenueId, Guid afterVenueId)
    {
        ArgumentNullException.ThrowIfNull(match);

        if (match.KickoffUtc.UtcDateTime != beforeKickoffUtc)
        {
            return new PushMessage(
                $"Ny tid {SideAndOpponent(match)}",
                When(match.KickoffUtc),
                Url(match.Id));
        }

        if (afterVenueId != beforeVenueId)
        {
            return new PushMessage(
                $"Ny plats {SideAndOpponent(match)}",
                match.Venue.Name,
                Url(match.Id));
        }

        return null;
    }

    private static string SideAndOpponent(MatchDto match) =>
        $"{(match.IsHome ? "hemma" : "borta")} mot {match.Opponent}";

    private static string When(DateTimeOffset kickoffUtc)
    {
        var local = SwedishTime.ToSwedish(kickoffUtc.UtcDateTime);

        // T.ex. "lördag 8 oktober kl. 14:00".
        return local.ToString("dddd d MMMM 'kl.' HH:mm", Swedish);
    }

    private static string Url(Guid matchId) => $"/match/{matchId}";
}
