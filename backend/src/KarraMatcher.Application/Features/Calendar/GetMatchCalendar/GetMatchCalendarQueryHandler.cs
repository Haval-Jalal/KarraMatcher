using System.Globalization;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Common;

namespace KarraMatcher.Application.Features.Calendar.GetMatchCalendar;

internal sealed class GetMatchCalendarQueryHandler(IMatchRepository matches)
    : IQueryHandler<GetMatchCalendarQuery, MatchCalendar?>
{
    public async Task<MatchCalendar?> HandleAsync(
        GetMatchCalendarQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var match = await matches.FindByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);

        if (match?.Team is null)
        {
            return null;
        }

        var content = IcsCalendarBuilder.BuildSingle(match.Team, match);

        return new MatchCalendar(content, FileNameFor(match.Team.Slug, match.KickoffUtc));
    }

    /// <summary>
    /// Filnamnet innehåller lag och matchdatum i svensk tid, t.ex. <c>karra-gul-2026-08-30.ics</c>.
    ///
    /// <para>
    /// Datumet är matchens dag så som föräldern uppfattar den (§KM.5). En avspark 00:30
    /// svensk tid ligger på föregående dygn i UTC, och en fil som heter fel dag är svår
    /// att hitta igen bland nedladdningarna.
    /// </para>
    /// </summary>
    private static string FileNameFor(string slug, DateTime kickoffUtc)
    {
        var swedishDay = SwedishTime.ToSwedish(kickoffUtc);

        return $"karra-{slug}-{swedishDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.ics";
    }
}
