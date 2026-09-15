using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Erbjudandet: lägga upp, se, dra tillbaka.
///
/// <h3>Ägarskapet prövas här, inte i controllern</h3>
///
/// <para>
/// Bara den som lade upp ett erbjudande får dra tillbaka det. Kontrollen sitter i samma
/// metod som ändringen, eftersom en kontroll som ligger utanför går att glömma nästa gång
/// någon anropar tjänsten från ett annat ställe.
/// </para>
///
/// <h3>Fritexten loggas aldrig</h3>
///
/// <para>
/// Audit-raden bär vad som hände och vem, aldrig notisen (§KM.10, §KM.12). Antalet platser
/// och riktningen är inte personuppgifter och hjälper den som en dag ska förstå vad som
/// hände; förarens egna ord gör det inte.
/// </para>
/// </summary>
public sealed class CarpoolOfferService(
    ICarpoolOfferRepository offers,
    ICarpoolRequestRepository requests,
    IAccountRepository accounts,
    IAuditLog audit,
    IPushOutbox push)
{
    /// <summary>
    /// Lägger upp ett erbjudande. Svarar null när matchen inte finns.
    /// </summary>
    public async Task<CarpoolOfferDto?> CreateAsync(
        Guid matchId,
        CarpoolOfferDraft draft,
        Guid driverAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var teamId = await offers.FindMatchTeamIdAsync(matchId, cancellationToken)
            .ConfigureAwait(false);

        if (teamId is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;

        var offer = new CarpoolOffer
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            DriverAccountId = driverAccountId,
            Direction = draft.Direction,
            DeparturePlace = draft.DeparturePlace.Trim(),
            DepartureUtc = draft.DepartureUtc,
            Seats = draft.Seats,
            Note = Blank(draft.Note),
            Status = CarpoolOfferStatus.Open,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await offers.AddAsync(offer, cancellationToken).ConfigureAwait(false);

        await audit.RecordAsync(
            AuditActions.CarpoolOfferCreated,
            driverAccountId,
            cancellationToken,
            offer.Id,
            Describe(offer)).ConfigureAwait(false);

        await offers.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Till lagets prenumeranter (§KM.12). Köas, aldrig skickat i requesten (§KM.11).
        push.Enqueue(PushDispatch.ToTeam(teamId.Value, CarpoolNotification.NewOffer(matchId)));

        return CarpoolOfferDto.For(offer, driverAccountId);
    }

    /// <summary>
    /// Matchens öppna erbjudanden, sedda av <paramref name="reader"/> (null = gäst).
    ///
    /// <para>
    /// Fulla erbjudanden är med. De märks ut med <see cref="CarpoolOfferDto.IsFull"/> och
    /// går fortfarande att fråga om (§KM.12) — att sortera bort dem hade tagit ifrån
    /// föraren möjligheten att svara "någon annan hann före".
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CarpoolOfferDto>> ListAsync(
        Guid matchId,
        Guid? reader,
        CancellationToken cancellationToken)
    {
        var open = await offers.ListOpenForMatchAsync(matchId, cancellationToken)
            .ConfigureAwait(false);

        if (open.Count == 0)
        {
            return [];
        }

        // En fraga for hela listan, inte en per erbjudande.
        var taken = await requests
            .AcceptedSeatsForOffersAsync([.. open.Select(offer => offer.Id)], cancellationToken)
            .ConfigureAwait(false);

        /*
         * Namnen hamtas bara at en inloggad lasare. En gast far listan utan dem (§KM.3) --
         * och da finns ingen anledning att fraga databasen efter dem heller.
         */
        var names = reader is null
            ? new Dictionary<Guid, string>()
            : await accounts
                .DisplayNamesAsync([.. open.Select(offer => offer.DriverAccountId).Distinct()], cancellationToken)
                .ConfigureAwait(false);

        return
        [
            .. open.Select(offer => CarpoolOfferDto.For(
                offer,
                reader,
                taken.TryGetValue(offer.Id, out var seats) ? seats : 0,
                names.TryGetValue(offer.DriverAccountId, out var name) ? name : null)),
        ];
    }

    /// <summary>
    /// Drar tillbaka ett erbjudande.
    /// </summary>
    /// <returns>
    /// Sant när det drogs tillbaka. Falskt både när erbjudandet inte finns och när det
    /// tillhör någon annan — <b>samma svar med flit</b>, annars går det att räkna ut vilka
    /// erbjudande-id som existerar genom att prova sig fram.
    /// </returns>
    public async Task<bool> WithdrawAsync(
        Guid offerId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var offer = await offers.FindForUpdateAsync(offerId, cancellationToken)
            .ConfigureAwait(false);

        if (offer is null || offer.DriverAccountId != actorAccountId)
        {
            return false;
        }

        if (offer.Status == CarpoolOfferStatus.Withdrawn)
        {
            // Redan tillbakadraget. Svarar ja utan att skriva en rad till: att dra tillbaka
            // nagot som redan ar tillbakadraget ar inte ett fel, och en andra audit-rad
            // hade beskrivit en handelse som inte intraffade.
            return true;
        }

        offer.Status = CarpoolOfferStatus.Withdrawn;
        offer.UpdatedUtc = DateTime.UtcNow;

        await audit.RecordAsync(
            AuditActions.CarpoolOfferWithdrawn,
            actorAccountId,
            cancellationToken,
            offer.Id).ConfigureAwait(false);

        await offers.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        /*
         * Till dem som redan accepterats -- de har planerat en resa som just föll (§KM.12).
         * Bara accepterade: den som bara frågat har ingen plats att förlora. Ingen fritext,
         * bara att erbjudandet drogs tillbaka.
         */
        var accepted = (await requests.ListForOfferAsync(offer.Id, cancellationToken)
                .ConfigureAwait(false))
            .Where(r => r.Status == CarpoolRequestStatus.Accepted)
            .Select(r => r.RequesterAccountId)
            .Distinct()
            .ToArray();

        if (accepted.Length > 0)
        {
            push.Enqueue(PushDispatch.ToAccounts(
                accepted, CarpoolNotification.OfferWithdrawn(offer.MatchId)));
        }

        return true;
    }

    /// <summary>
    /// Audit-radens text. <b>Notisen är inte med</b>, och får aldrig bli det (§KM.10).
    /// </summary>
    internal static string Describe(CarpoolOffer offer)
    {
        ArgumentNullException.ThrowIfNull(offer);

        return $"{offer.Direction}, {offer.Seats} platser, avgång {offer.DepartureUtc:O}";
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
