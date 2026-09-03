using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Persistence;
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
public sealed class CarpoolOfferService(ICarpoolOfferRepository offers, IAuditLog audit)
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

        if (!await offers.MatchExistsAsync(matchId, cancellationToken).ConfigureAwait(false))
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

        return CarpoolOfferDto.For(offer, driverAccountId);
    }

    /// <summary>Matchens öppna erbjudanden, sedda av <paramref name="reader"/> (null = gäst).</summary>
    public async Task<IReadOnlyList<CarpoolOfferDto>> ListAsync(
        Guid matchId,
        Guid? reader,
        CancellationToken cancellationToken)
    {
        var open = await offers.ListOpenForMatchAsync(matchId, cancellationToken)
            .ConfigureAwait(false);

        return [.. open.Select(offer => CarpoolOfferDto.For(offer, reader))];
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
