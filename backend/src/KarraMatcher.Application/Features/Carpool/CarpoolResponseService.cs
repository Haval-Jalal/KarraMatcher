using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>Vad ett försök att svara på en förfrågan slutade med.</summary>
public enum CarpoolResponseOutcome
{
    Answered = 0,

    /// <summary>
    /// Förfrågan finns inte — eller så är det inte den svarandes erbjudande.
    ///
    /// <para>
    /// Ett och samma utfall med flit. Skiljde de sig åt kunde vem som helst kartlägga vilka
    /// förfrågnings-id som existerar genom att prova sig fram.
    /// </para>
    /// </summary>
    NotFound = 1,

    /// <summary>Erbjudandet är tillbakadraget. Då finns det inget att svara på.</summary>
    OfferUnavailable = 2,

    /// <summary>Redan accepterad eller nekad.</summary>
    AlreadyAnswered = 3,

    /// <summary>Den som frågade har återtagit den.</summary>
    Retracted = 4,

    /// <summary>Accepten skulle spränga antalet platser i bilen.</summary>
    NotEnoughSeats = 5,
}

/// <summary>
/// Förarens svar: acceptera eller neka (§KM.12).
///
/// <h3>Skild från <see cref="CarpoolRequestService"/></h3>
///
/// <para>
/// Den tjänsten är den frågandes sida — skicka, se, återta. Den här är förarens, och de två
/// har olika ägare av samma rad: förfrågan tillhör den som frågade, svaret tillhör den som
/// äger erbjudandet. Att hålla isär dem gör att ägarkontrollen inte kan råka bli fel sort.
/// </para>
///
/// <h3>Platserna räknas här, aldrig i gränssnittet</h3>
///
/// <para>
/// Bara accepterade förfrågningar förbrukar platser. En accept som skulle överskrida
/// erbjudandets antal avvisas med ett begripligt fel — inte genom att knappen döljs, för en
/// dold knapp är ingen regel.
/// </para>
///
/// <h3>Ett nej bär alltid ett meddelande</h3>
///
/// <para>
/// Kravet sitter i valideringen av kommandot, alltså före den här tjänsten. Det är en
/// modellregel och inte en artighet i gränssnittet: det är en granne man möter på planen
/// nästa lördag.
/// </para>
/// </summary>
public sealed class CarpoolResponseService(
    ICarpoolRequestRepository requests,
    ICarpoolOfferRepository offers,
    IAuditLog audit)
{
    /// <summary>
    /// Föraren säger ja. Meddelandet är valfritt här — ett ja behöver inga ord.
    /// </summary>
    /// <returns>
    /// Utfallet. Andra värdet är antalet platser som faktiskt är kvar och betyder något
    /// <b>bara</b> tillsammans med <see cref="CarpoolResponseOutcome.NotEnoughSeats"/> —
    /// felet ska kunna säga hur många som fanns, inte bara att det var för många.
    /// </returns>
    public Task<(CarpoolResponseOutcome Outcome, int SeatsLeft)> AcceptAsync(
        Guid requestId,
        string? message,
        Guid actorAccountId,
        CancellationToken cancellationToken) =>
        AnswerAsync(
            requestId, CarpoolRequestStatus.Accepted, message, actorAccountId, cancellationToken);

    /// <summary>Föraren säger nej. Meddelandet är obligatoriskt och validerat före hit.</summary>
    public Task<(CarpoolResponseOutcome Outcome, int SeatsLeft)> DenyAsync(
        Guid requestId,
        string message,
        Guid actorAccountId,
        CancellationToken cancellationToken) =>
        AnswerAsync(
            requestId, CarpoolRequestStatus.Denied, message, actorAccountId, cancellationToken);

    private async Task<(CarpoolResponseOutcome Outcome, int SeatsLeft)> AnswerAsync(
        Guid requestId,
        CarpoolRequestStatus answer,
        string? message,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var request = await requests.FindForUpdateAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (request is null)
        {
            return (CarpoolResponseOutcome.NotFound, 0);
        }

        var offer = await offers.FindForUpdateAsync(request.OfferId, cancellationToken)
            .ConfigureAwait(false);

        /*
         * Agarkontrollen. Den sitter har och inte i controllern: en kontroll som ligger
         * utanfor metoden gar att glomma nasta gang tjansten anropas fran ett annat stalle.
         */
        if (offer is null || offer.DriverAccountId != actorAccountId)
        {
            return (CarpoolResponseOutcome.NotFound, 0);
        }

        if (offer.Status != CarpoolOfferStatus.Open)
        {
            return (CarpoolResponseOutcome.OfferUnavailable, 0);
        }

        if (request.Status == CarpoolRequestStatus.Retracted)
        {
            return (CarpoolResponseOutcome.Retracted, 0);
        }

        /*
         * Ett svar ges en gang. Till skillnad fran att atertaga -- dar ett andra forsok ar
         * ofarligt och svarar ja -- betyder ett andra svar att foraren andrar sig, och det
         * ar inte samma handelse. Den som vill andra sig far gora det med orden, inte genom
         * att trycka igen.
         */
        if (request.Status != CarpoolRequestStatus.Pending)
        {
            return (CarpoolResponseOutcome.AlreadyAnswered, 0);
        }

        var seatsLeft = 0;

        if (answer == CarpoolRequestStatus.Accepted)
        {
            var taken = await requests.AcceptedSeatsAsync(request.OfferId, cancellationToken)
                .ConfigureAwait(false);

            seatsLeft = Math.Max(0, offer.Seats - taken);

            if (request.Seats > seatsLeft)
            {
                return (CarpoolResponseOutcome.NotEnoughSeats, seatsLeft);
            }
        }

        request.Status = answer;
        request.ResponseMessage = Blank(message);
        request.UpdatedUtc = DateTime.UtcNow;

        // Forarens ord ar inte med. Fritext loggas aldrig (§KM.10, §KM.12).
        await audit.RecordAsync(
            answer == CarpoolRequestStatus.Accepted
                ? AuditActions.CarpoolRequestAccepted
                : AuditActions.CarpoolRequestDenied,
            actorAccountId,
            cancellationToken,
            request.Id,
            $"{request.Seats} platser").ConfigureAwait(false);

        await requests.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // SeatsLeft bar bara betydelse tillsammans med NotEnoughSeats -- se metodens summary.
        return (CarpoolResponseOutcome.Answered, 0);
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
