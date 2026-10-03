using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Carpool;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>Vad ett försök att be om skjuts slutade med.</summary>
public enum CarpoolRideRequestOutcome
{
    Created = 0,

    /// <summary>Händelsen finns inte.</summary>
    EventNotFound = 1,

    /// <summary>Händelsen är inte en match — samåkning gäller bara matcher (§KM.12).</summary>
    NotAMatch = 2,
}

/// <summary>Vad ett försök att erbjuda plats på en skjutsförfrågan slutade med.</summary>
public enum CarpoolRideOfferOutcome
{
    Created = 0,

    /// <summary>Förfrågan finns inte, är löst eller tillbakadragen.</summary>
    RequestUnavailable = 1,

    /// <summary>Den som frågade kan inte erbjuda plats åt sig själv.</summary>
    OwnRequest = 2,

    /// <summary>Föraren har redan ett platserbjudande som väntar eller är accepterat.</summary>
    AlreadyOffered = 3,
}

/// <summary>Vad ett försök att svara på ett platserbjudande slutade med.</summary>
public enum CarpoolRideAnswerOutcome
{
    Answered = 0,

    /// <summary>Erbjudandet finns inte — eller så är det inte den svarandes förfrågan. Samma svar med flit.</summary>
    NotFound = 1,

    /// <summary>Förfrågan är inte längre öppen (redan löst eller tillbakadragen).</summary>
    RequestUnavailable = 2,

    /// <summary>Platserbjudandet är redan accepterat, nekat eller återtaget.</summary>
    AlreadyAnswered = 3,
}

/// <summary>
/// Skjutsförfrågan: en förälder ber om skjuts, och förare erbjuder plats på det (§KM.12, `#63`).
///
/// <h3>Spegelbilden av erbjudande/åkförfrågan</h3>
///
/// <para>
/// Två sidor delar samma rader men har olika ägare: skjutsförfrågan tillhör den som frågade (bara
/// hen drar tillbaka den och svarar på platserbjudanden), platserbjudandet tillhör föraren (bara
/// hen återtar det). Ägarkontrollen sitter i varje metod, inte i controllern — en kontroll utanför
/// metoden går att glömma nästa gång tjänsten anropas från ett annat ställe.
/// </para>
///
/// <h3>Ett nej bär alltid ett meddelande</h3>
///
/// <para>
/// Kravet sitter i valideringen av <see cref="CarpoolRideCommands"/>, före den här tjänsten — en
/// modellregel (§KM.12), inte en artighet i gränssnittet.
/// </para>
/// </summary>
public sealed class CarpoolRideService(
    ICarpoolRideRepository rides,
    ICarpoolOfferRepository offers,
    IAccountRepository accounts,
    IAuditLog audit,
    IPushOutbox push)
{
    // ---- Skjutsförfrågan (passagerarens sida) ----------------------------------------

    /// <summary>En förälder ber om skjuts. Avvisar en händelse som inte finns eller inte är en match.</summary>
    public async Task<(CarpoolRideRequestOutcome Outcome, CarpoolRideRequestDto? Request)>
        CreateRequestAsync(
            Guid matchId,
            CarpoolRideRequestDraft draft,
            Guid requesterAccountId,
            CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var target = await offers.FindEventTargetAsync(matchId, cancellationToken)
            .ConfigureAwait(false);

        if (target is null)
        {
            return (CarpoolRideRequestOutcome.EventNotFound, null);
        }

        // Samåkning gäller bara matcher (§KM.12) — samma gräns som erbjudandet. FE döljer sektionen
        // för allt annat; det här är den riktiga grinden.
        if (target.Type != EventType.Match)
        {
            return (CarpoolRideRequestOutcome.NotAMatch, null);
        }

        var now = DateTime.UtcNow;

        var request = new CarpoolRideRequest
        {
            Id = Guid.NewGuid(),
            MatchId = matchId,
            RequesterAccountId = requesterAccountId,
            Direction = draft.Direction,
            Seats = draft.Seats,
            Note = Blank(draft.Note),
            Status = CarpoolRideRequestStatus.Open,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await rides.AddRequestAsync(request, cancellationToken).ConfigureAwait(false);

        // Notisen loggas aldrig — bara riktning/antal, aldrig fritexten (§KM.10, §KM.12).
        await audit.RecordAsync(
            AuditActions.CarpoolRideRequestCreated,
            requesterAccountId,
            cancellationToken,
            request.Id,
            $"{request.Direction}, {request.Seats} platser").ConfigureAwait(false);

        await rides.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Till lagets prenumeranter, som ett nytt erbjudande (§KM.12). Trupp-vida händelser saknar
        // lag att pusha till; samåkning finns bara på matcher, men guardas ändå.
        if (target.TeamId is Guid teamId)
        {
            push.Enqueue(PushDispatch.ToTeam(
                teamId, PushCategory.Carpool, CarpoolNotification.NewRideRequest(matchId)));
        }

        return (
            CarpoolRideRequestOutcome.Created,
            CarpoolRideRequestDto.For(request, requesterAccountId));
    }

    /// <summary>Matchens öppna skjutsförfrågningar, med namnet på den som frågade.</summary>
    public async Task<IReadOnlyList<CarpoolRideRequestDto>> ListRequestsAsync(
        Guid matchId,
        Guid reader,
        CancellationToken cancellationToken)
    {
        var all = await rides.ListOpenForMatchAsync(matchId, cancellationToken).ConfigureAwait(false);

        if (all.Count == 0)
        {
            return [];
        }

        var names = await accounts
            .DisplayNamesAsync([.. all.Select(r => r.RequesterAccountId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. all.Select(r => CarpoolRideRequestDto.For(
                r, reader, names.TryGetValue(r.RequesterAccountId, out var name) ? name : null)),
        ];
    }

    /// <summary>
    /// Drar tillbaka en skjutsförfrågan.
    /// </summary>
    /// <returns>
    /// Sant när den drogs tillbaka. Falskt både när den inte finns och när den tillhör någon annan
    /// — samma svar med flit, annars går det att kartlägga vilka id som finns.
    /// </returns>
    public async Task<bool> WithdrawRequestAsync(
        Guid requestId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var request = await rides.FindRequestForUpdateAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (request is null || request.RequesterAccountId != actorAccountId)
        {
            return false;
        }

        if (request.Status == CarpoolRideRequestStatus.Withdrawn)
        {
            // Redan tillbakadragen — ja utan en andra audit-rad.
            return true;
        }

        request.Status = CarpoolRideRequestStatus.Withdrawn;
        request.UpdatedUtc = DateTime.UtcNow;

        // Förare med ett aktivt platserbjudande ska veta att det inte längre finns något att köra.
        var activeDrivers = (await rides.ListOffersForRequestAsync(requestId, cancellationToken)
            .ConfigureAwait(false))
            .Where(o => o.IsActive)
            .Select(o => o.DriverAccountId)
            .Distinct()
            .ToArray();

        await audit.RecordAsync(
            AuditActions.CarpoolRideRequestWithdrawn,
            actorAccountId,
            cancellationToken,
            request.Id).ConfigureAwait(false);

        await rides.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (activeDrivers.Length > 0)
        {
            push.Enqueue(PushDispatch.ToAccounts(
                activeDrivers, PushCategory.Carpool,
                CarpoolNotification.RideRequestWithdrawn(request.MatchId)));
        }

        return true;
    }

    // ---- Platserbjudande (förarens sida) ---------------------------------------------

    /// <summary>En förare erbjuder plats på en skjutsförfrågan.</summary>
    public async Task<(CarpoolRideOfferOutcome Outcome, CarpoolRideOfferDto? Offer)> OfferSeatAsync(
        Guid matchId,
        Guid rideRequestId,
        CarpoolRideOfferDraft draft,
        Guid driverAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var request = await rides.FindRequestForUpdateAsync(rideRequestId, cancellationToken)
            .ConfigureAwait(false);

        /*
         * Förfrågan måste finnas, höra till matchen i route:n (§KM.3, jfr `#470`) och vara öppen.
         * Samma tysta "finns inte"-svar för en löst/tillbakadragen förfrågan eller en i en annan
         * match — den som frisatt sin plats ska inte få fler erbjudanden, och en gissare ska inte
         * kunna kartlägga vilka id som finns.
         */
        if (request is null || request.MatchId != matchId || !request.IsOpen)
        {
            return (CarpoolRideOfferOutcome.RequestUnavailable, null);
        }

        if (request.RequesterAccountId == driverAccountId)
        {
            return (CarpoolRideOfferOutcome.OwnRequest, null);
        }

        if (await rides.HasActiveOfferAsync(rideRequestId, driverAccountId, cancellationToken)
            .ConfigureAwait(false))
        {
            return (CarpoolRideOfferOutcome.AlreadyOffered, null);
        }

        var now = DateTime.UtcNow;

        var offer = new CarpoolRideOffer
        {
            Id = Guid.NewGuid(),
            RideRequestId = rideRequestId,
            DriverAccountId = driverAccountId,
            Seats = draft.Seats,
            Message = Blank(draft.Message),
            Status = CarpoolRequestStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await rides.AddOfferAsync(offer, cancellationToken).ConfigureAwait(false);

        await audit.RecordAsync(
            AuditActions.CarpoolRideOfferCreated,
            driverAccountId,
            cancellationToken,
            offer.Id,
            $"{offer.Seats} platser").ConfigureAwait(false);

        await rides.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Till den som frågade, inte till laget (§KM.12). Ingen hälsning — fritext når aldrig en
        // låsskärm, bara "öppna för att svara".
        push.Enqueue(PushDispatch.ToAccounts(
            [request.RequesterAccountId], PushCategory.Carpool,
            CarpoolNotification.NewRideOffer(request.MatchId)));

        return (CarpoolRideOfferOutcome.Created, CarpoolRideOfferDto.For(offer, driverAccountId));
    }

    /// <summary>
    /// En skjutsförfrågans platserbjudanden, sedda av <paramref name="reader"/>.
    ///
    /// <para>
    /// Den som frågade ser alla — det är hen som ska svara. En förare ser bara sitt eget. Hälsningen
    /// är fritext och får bara nå de inblandade (§KM.12), så filtreringen sitter här.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CarpoolRideOfferDto>> ListOffersAsync(
        Guid rideRequestId,
        Guid reader,
        CancellationToken cancellationToken)
    {
        var request = await rides.FindRequestForUpdateAsync(rideRequestId, cancellationToken)
            .ConfigureAwait(false);

        if (request is null)
        {
            return [];
        }

        var all = await rides.ListOffersForRequestAsync(rideRequestId, cancellationToken)
            .ConfigureAwait(false);

        var visible = request.RequesterAccountId == reader
            ? all
            : [.. all.Where(o => o.DriverAccountId == reader)];

        var names = await accounts
            .DisplayNamesAsync([.. visible.Select(o => o.DriverAccountId).Distinct()], cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. visible.Select(o => CarpoolRideOfferDto.For(
                o, reader, names.TryGetValue(o.DriverAccountId, out var name) ? name : null)),
        ];
    }

    /// <summary>Den som frågade tackar ja till ett platserbjudande. Förfrågan blir löst.</summary>
    public Task<CarpoolRideAnswerOutcome> AcceptOfferAsync(
        Guid offerId,
        string? message,
        Guid actorAccountId,
        CancellationToken cancellationToken) =>
        AnswerAsync(offerId, CarpoolRequestStatus.Accepted, message, actorAccountId, cancellationToken);

    /// <summary>Den som frågade tackar nej. Meddelandet är obligatoriskt och validerat före hit.</summary>
    public Task<CarpoolRideAnswerOutcome> DenyOfferAsync(
        Guid offerId,
        string message,
        Guid actorAccountId,
        CancellationToken cancellationToken) =>
        AnswerAsync(offerId, CarpoolRequestStatus.Denied, message, actorAccountId, cancellationToken);

    private async Task<CarpoolRideAnswerOutcome> AnswerAsync(
        Guid offerId,
        CarpoolRequestStatus answer,
        string? message,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var offer = await rides.FindOfferForUpdateAsync(offerId, cancellationToken)
            .ConfigureAwait(false);

        if (offer is null)
        {
            return CarpoolRideAnswerOutcome.NotFound;
        }

        var request = await rides.FindRequestForUpdateAsync(offer.RideRequestId, cancellationToken)
            .ConfigureAwait(false);

        /*
         * Ägarkontrollen: bara den som äger skjutsförfrågan svarar på dess platserbjudanden. Samma
         * "finns inte"-svar som för ett saknat erbjudande — annars går det att kartlägga id.
         */
        if (request is null || request.RequesterAccountId != actorAccountId)
        {
            return CarpoolRideAnswerOutcome.NotFound;
        }

        if (!request.IsOpen)
        {
            return CarpoolRideAnswerOutcome.RequestUnavailable;
        }

        // Ett svar ges en gång. Att ändra sig görs med orden, inte genom att trycka igen.
        if (offer.Status != CarpoolRequestStatus.Pending)
        {
            return CarpoolRideAnswerOutcome.AlreadyAnswered;
        }

        var now = DateTime.UtcNow;

        offer.Status = answer;
        offer.ResponseMessage = Blank(message);
        offer.UpdatedUtc = now;

        // En accept löser förfrågan: någon kör. Övriga väntande erbjudanden kan då inte längre
        // accepteras (förfrågan är inte öppen) — förarna ser att den är löst och kan återta sina.
        if (answer == CarpoolRequestStatus.Accepted)
        {
            request.Status = CarpoolRideRequestStatus.Fulfilled;
            request.UpdatedUtc = now;
        }

        // Svaret loggas utan fritext (§KM.10, §KM.12).
        await audit.RecordAsync(
            answer == CarpoolRequestStatus.Accepted
                ? AuditActions.CarpoolRideOfferAccepted
                : AuditActions.CarpoolRideOfferDenied,
            actorAccountId,
            cancellationToken,
            offer.Id,
            $"{offer.Seats} platser").ConfigureAwait(false);

        await rides.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Till föraren som erbjöd plats — accept och nekande båda (§KM.12). Aldrig meddelandet.
        push.Enqueue(PushDispatch.ToAccounts(
            [offer.DriverAccountId], PushCategory.Carpool,
            CarpoolNotification.RideOfferAnswered(request.MatchId)));

        return CarpoolRideAnswerOutcome.Answered;
    }

    /// <summary>
    /// Föraren återtar sitt platserbjudande.
    /// </summary>
    /// <returns>Sant när det återtogs; falskt när det inte finns eller tillhör någon annan (samma svar).</returns>
    public async Task<bool> RetractOfferAsync(
        Guid offerId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var offer = await rides.FindOfferForUpdateAsync(offerId, cancellationToken)
            .ConfigureAwait(false);

        if (offer is null || offer.DriverAccountId != actorAccountId)
        {
            return false;
        }

        if (offer.Status == CarpoolRequestStatus.Retracted)
        {
            return true;
        }

        offer.Status = CarpoolRequestStatus.Retracted;
        offer.UpdatedUtc = DateTime.UtcNow;

        await audit.RecordAsync(
            AuditActions.CarpoolRideOfferRetracted,
            actorAccountId,
            cancellationToken,
            offer.Id).ConfigureAwait(false);

        await rides.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
