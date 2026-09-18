using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Tränarens överblick över samåkningen i sitt lag (`#55`, §KM.12).
///
/// <h3>Framåt, inte bakåt</h3>
///
/// <para>
/// Bara matcher som ännu inte spelats. Tränarens fråga är om någon behöver skjuts på
/// lördag, inte vem som körde i maj — och det som spelats gallras ändå bort trettio dagar
/// senare (se <see cref="CarpoolRetentionService"/>).
/// </para>
///
/// <h3>Inställda matcher räknas inte</h3>
///
/// <para>
/// Ingen behöver skjuts till en match som inte spelas. Att lista den som "saknar förare"
/// hade varit ett larm om ingenting, och larm om ingenting är hur en överblick slutar
/// läsas.
/// </para>
/// </summary>
public sealed class CarpoolOverviewService(
    ITeamRepository teams,
    ICarpoolOfferRepository offers,
    ICarpoolRequestRepository requests,
    IAccountRepository accounts,
    TimeProvider clock)
{
    /// <summary>Lagets kommande matcher med sin samåkning. Null när laget inte finns.</summary>
    public async Task<IReadOnlyList<TeamCarpoolMatchDto>?> ForTeamAsync(
        string slug,
        Guid? reader,
        CancellationToken cancellationToken)
    {
        var team = await teams.FindBySlugAsync(slug, cancellationToken).ConfigureAwait(false);

        if (team is null)
        {
            return null;
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // Samåkningen gäller matcher (§KM.12). Träningar och övriga händelser ligger i samma
        // tabell sedan `#198`, men samåkningsöverblicken är matchorienterad — filtrera hit.
        var upcoming = (await teams.GetEventsAsync(team.Id, cancellationToken).ConfigureAwait(false))
            .Where(item => item.Type == EventType.Match
                && item.KickoffUtc >= now
                && item.Status != EventStatus.Cancelled)
            .OrderBy(item => item.KickoffUtc)
            .ToList();

        if (upcoming.Count == 0)
        {
            return [];
        }

        var open = await offers
            .ListOpenForMatchesAsync([.. upcoming.Select(match => match.Id)], cancellationToken)
            .ConfigureAwait(false);

        /*
         * Tva fragor for hela listan i stallet for tva per erbjudande. Overblicken visar en
         * hel sasongsrest, och en fraga per rad hade blivit tiotals anrop mot en databas som
         * ligger hos Neon och en backend som just vaknat ur en kallstart (§KM.11).
         */
        var offerIds = open.Select(offer => offer.Id).ToArray();

        var taken = await requests
            .AcceptedSeatsForOffersAsync(offerIds, cancellationToken)
            .ConfigureAwait(false);

        var pending = await requests
            .CountPendingForOffersAsync(offerIds, cancellationToken)
            .ConfigureAwait(false);

        // Tranaren ar inloggad, sa namnen foljer med. Overblicken var raknad och inte
        // namngiven fore #154 -- nu kan den svara pa vem som kor.
        var names = await accounts
            .DisplayNamesAsync([.. open.Select(offer => offer.DriverAccountId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. upcoming.Select(match =>
            {
                var forMatch = open
                    .Where(offer => offer.MatchId == match.Id)
                    .Select(offer => CarpoolOfferDto.For(
                        offer,
                        reader,
                        taken.TryGetValue(offer.Id, out var seats) ? seats : 0,
                        names.TryGetValue(offer.DriverAccountId, out var name) ? name : null))
                    .ToArray();

                var waiting = forMatch.Sum(offer =>
                    pending.TryGetValue(offer.Id, out var count) ? count : 0);

                return new TeamCarpoolMatchDto(
                    match.Id,
                    match.KickoffUtc,
                    match.OpponentName ?? string.Empty,
                    match.IsHome ?? false,
                    forMatch,
                    waiting);
            }),
        ];
    }
}
