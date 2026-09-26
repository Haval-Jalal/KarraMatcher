using System.Globalization;

using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Events.Admin;
using KarraMatcher.Domain.Common;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Events.Import;

/// <summary>Vad en förhandsgranskning eller import gav.</summary>
public sealed record ImportResult(IReadOnlyList<ParsedLine> Lines, int Imported);

/// <summary>
/// Kopplar parsern till verkligheten: lagen, spelplatserna och det som redan finns.
///
/// <para>
/// Ett inklistrat serieschema är alltid matcher, så importen skapar händelser av typen
/// <see cref="EventType.Match"/>.
/// </para>
///
/// <para>
/// <b>Inklistringen tolkas om vid import.</b> Klienten skickar samma text en gång till,
/// aldrig den tolkade listan. Skulle servern spara det klienten säger att texten betydde
/// vore förhandsgranskningen en rekommendation — och en tränare för Gul kunde skicka en
/// lista med Blås matcher.
/// </para>
///
/// <para>
/// <b>Bara det egna lagets rader importeras.</b> Ett inklistrat serieschema innehåller
/// ofta alla fyra lagen, och behörigheten gäller ett. Rader för andra lag rapporteras och
/// hoppas över i stället för att tyst försvinna.
/// </para>
/// </summary>
public sealed class ScheduleImportService(
    IScheduleImportRepository repository,
    EventAdminService events)
{
    /// <summary>Tolkar utan att spara någonting.</summary>
    public async Task<ImportResult> PreviewAsync(
        string teamSlug,
        string? pasted,
        CancellationToken cancellationToken)
    {
        var lines = await ParseAsync(teamSlug, pasted, cancellationToken).ConfigureAwait(false);

        return new ImportResult(lines, 0);
    }

    /// <summary>
    /// Tolkar om och sparar de rader som gick igenom.
    ///
    /// <para>
    /// Delvis import är avsiktlig: en trasig rad ska inte hindra de tjugofyra som är rätt.
    /// Tränaren ser vilka som hoppades över och kan rätta dem för hand.
    /// </para>
    /// </summary>
    public async Task<ImportResult> ImportAsync(
        string teamSlug,
        string? pasted,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var lines = await ParseAsync(teamSlug, pasted, cancellationToken).ConfigureAwait(false);
        var imported = 0;

        foreach (var line in lines.Where(line => line.Outcome == LineOutcome.Ok))
        {
            var match = line.Match!;

            /*
             * Gar via samma tjanst som ett enskilt tillagg. Det ger varje handelse sin egen
             * audit-post och samma regler -- att skriva en genvag har hade varit att bygga
             * en andra vag in i databasen med hälften av kontrollerna.
             */
            var result = await events.CreateAsync(
                teamSlug,
                new EventDraft(
                    EventType.Match,
                    match.KickoffUtc,
                    Title: null,
                    match.Opponent,
                    IsHome: true,
                    Address: null,
                    Note: null,
                    VenueId: match.VenueId),
                actorAccountId,
                cancellationToken).ConfigureAwait(false);

            if (result.Outcome == EventSaveOutcome.Ok)
            {
                imported++;
            }
        }

        return new ImportResult(lines, imported);
    }

    private async Task<IReadOnlyList<ParsedLine>> ParseAsync(
        string teamSlug,
        string? pasted,
        CancellationToken cancellationToken)
    {
        var world = await repository.LoadAsync(teamSlug, cancellationToken).ConfigureAwait(false);

        var context = new ScheduleContext(
            world.TeamsByName,
            world.VenuesByName,
            world.ExistingMatchKeys,
            ToUtc);

        var lines = ScheduleParser.Parse(pasted, context);

        // Rader for andra lag: rapporteras, inte importeras. Behorigheten galler ett lag.
        return
        [
            .. lines.Select(line =>
                line.Outcome == LineOutcome.Ok && line.Match!.TeamSlug != teamSlug
                    ? line with
                    {
                        Outcome = LineOutcome.OtherTeam,
                        Match = null,
                        Problem = "Raden gäller ett annat lag och hoppas över.",
                    }
                    : line),
        ];
    }

    /// <summary>
    /// Svensk lokaltid till UTC, genom domänens enda omräkning (§KM.5).
    ///
    /// <para>
    /// Svarar null när tiden inte finns — timmen som hoppas över när klockan ställs fram.
    /// Parsern gör då raden till ett besked i stället för att gissa sig förbi.
    /// </para>
    /// </summary>
    private static string? ToUtc(string localDateAndTime)
    {
        var parts = localDateAndTime.Split('T');

        if (parts.Length != 2
            || !DateOnly.TryParse(parts[0], CultureInfo.InvariantCulture, out var date)
            || !TimeOnly.TryParse(parts[1], CultureInfo.InvariantCulture, out var time))
        {
            return null;
        }

        try
        {
            return SwedishTime.ToUtc(date, time)
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
