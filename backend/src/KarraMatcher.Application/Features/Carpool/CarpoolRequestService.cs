using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Audit;
using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>Vad ett försök att skicka en förfrågan slutade med.</summary>
public enum CarpoolRequestOutcome
{
    Created = 0,

    /// <summary>Erbjudandet finns inte, eller är tillbakadraget.</summary>
    OfferUnavailable = 1,

    /// <summary>Den som frågar har redan en förfrågan som väntar eller är accepterad.</summary>
    AlreadyAsked = 2,

    /// <summary>Föraren kan inte fråga sig själv.</summary>
    OwnOffer = 3,
}

/// <summary>
/// Åkförfrågan: skicka, se, återta.
///
/// <h3>Att fråga tar ingen plats</h3>
///
/// <para>
/// Föraren väljer vem som åker med — det är inte först till kvarn (§KM.12). En förfrågan
/// gör därför inget anspråk på erbjudandets platser; det gör först accepten, som byggs i
/// <c>#52</c>.
/// </para>
///
/// <h3>Fullt erbjudande hindrar inte en förfrågan</h3>
///
/// <para>
/// Avsiktligt, och värt att inte "rätta till" senare: föraren ska kunna svara "någon annan
/// hann före" i stället för att den som frågar möts av en död knapp. Tjänsten tittar därför
/// aldrig på hur många platser som är kvar.
/// </para>
/// </summary>
public sealed class CarpoolRequestService(
    ICarpoolRequestRepository requests,
    ICarpoolOfferRepository offers,
    IAuditLog audit)
{
    /// <summary>Skickar en förfrågan.</summary>
    public async Task<(CarpoolRequestOutcome Outcome, CarpoolRequestDto? Request)> CreateAsync(
        Guid offerId,
        CarpoolRequestDraft draft,
        Guid requesterAccountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var offer = await offers.FindForUpdateAsync(offerId, cancellationToken)
            .ConfigureAwait(false);

        /*
         * Tillbakadraget erbjudande gar inte att fraga om. Samma svar som for ett
         * erbjudande som inte finns -- den som frisatt sin plats ska inte fa fler
         * forfragningar, och en gissare ska inte kunna kartlagga vilka id som finns.
         */
        if (offer is null || offer.Status != CarpoolOfferStatus.Open)
        {
            return (CarpoolRequestOutcome.OfferUnavailable, null);
        }

        if (offer.DriverAccountId == requesterAccountId)
        {
            return (CarpoolRequestOutcome.OwnOffer, null);
        }

        if (await requests.HasActiveAsync(offerId, requesterAccountId, cancellationToken)
            .ConfigureAwait(false))
        {
            return (CarpoolRequestOutcome.AlreadyAsked, null);
        }

        var now = DateTime.UtcNow;

        var request = new CarpoolRequest
        {
            Id = Guid.NewGuid(),
            OfferId = offerId,
            RequesterAccountId = requesterAccountId,
            Seats = draft.Seats,
            Message = Blank(draft.Message),
            Status = CarpoolRequestStatus.Pending,
            CreatedUtc = now,
            UpdatedUtc = now,
        };

        await requests.AddAsync(request, cancellationToken).ConfigureAwait(false);

        // Hälsningen är inte med. Fritext loggas aldrig (§KM.10, §KM.12).
        await audit.RecordAsync(
            AuditActions.CarpoolRequestCreated,
            requesterAccountId,
            cancellationToken,
            request.Id,
            $"{request.Seats} platser").ConfigureAwait(false);

        await requests.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return (CarpoolRequestOutcome.Created, CarpoolRequestDto.For(request, requesterAccountId));
    }

    /// <summary>
    /// Förfrågningarna på ett erbjudande, sedda av <paramref name="reader"/>.
    ///
    /// <para>
    /// Föraren ser alla — det är hen som ska svara. Alla andra ser bara sina egna.
    /// Hälsningen är fritext och får bara nå de inblandade (§KM.12), så filtreringen sitter
    /// här och inte i gränssnittet.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CarpoolRequestDto>> ListAsync(
        Guid offerId,
        Guid reader,
        CancellationToken cancellationToken)
    {
        var offer = await offers.FindForUpdateAsync(offerId, cancellationToken)
            .ConfigureAwait(false);

        if (offer is null)
        {
            return [];
        }

        var all = await requests.ListForOfferAsync(offerId, cancellationToken)
            .ConfigureAwait(false);

        var visible = offer.DriverAccountId == reader
            ? all
            : [.. all.Where(r => r.RequesterAccountId == reader)];

        return [.. visible.Select(r => CarpoolRequestDto.For(r, reader))];
    }

    /// <summary>
    /// Återtar en förfrågan.
    /// </summary>
    /// <returns>
    /// Sant när den återtogs. Falskt både när den inte finns och när den tillhör någon
    /// annan — <b>samma svar med flit</b>, annars går det att kartlägga vilka id som finns.
    /// </returns>
    public async Task<bool> RetractAsync(
        Guid requestId,
        Guid actorAccountId,
        CancellationToken cancellationToken)
    {
        var request = await requests.FindForUpdateAsync(requestId, cancellationToken)
            .ConfigureAwait(false);

        if (request is null || request.RequesterAccountId != actorAccountId)
        {
            return false;
        }

        if (request.Status == CarpoolRequestStatus.Retracted)
        {
            // Redan atertagen. Ja utan en andra audit-rad: att atertaga nagot som redan ar
            // atertaget ar inte ett fel, och raden hade beskrivit en handelse som uteblev.
            return true;
        }

        request.Status = CarpoolRequestStatus.Retracted;
        request.UpdatedUtc = DateTime.UtcNow;

        await audit.RecordAsync(
            AuditActions.CarpoolRequestRetracted,
            actorAccountId,
            cancellationToken,
            request.Id).ConfigureAwait(false);

        await requests.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
