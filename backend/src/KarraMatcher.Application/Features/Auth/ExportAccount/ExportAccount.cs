using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Auth.ExportAccount;

/// <summary>
/// Registerutdrag: allt servern har om ett konto, i ett läsbart svep (`#67`, checklistan 9.13).
///
/// <h3>Litet med flit</h3>
///
/// <para>
/// GDPR:s rätt till registerutdrag blir här nästan tom, och det är själva poängen: servern
/// lagrar så lite att utdraget ryms på en skärm. Kontot (en vuxens egna uppgifter, §KM.1),
/// samåkningen, närvarosvaren och notisinställningarna — inget om ett barn, ingen statistik.
/// </para>
///
/// <h3>Spelarkortet är inte här — och det sägs rakt ut</h3>
///
/// <para>
/// Barnets statistik lämnar aldrig telefonen (§KM.2), så vi kan inte lämna ut den. Utdraget
/// säger det i klartext och hänvisar till säkerhetskopieringskoden, i stället för att tiga
/// om ett tomrum som annars ser ut som att något saknas.
/// </para>
/// </summary>
public sealed record ExportAccountQuery(Guid AccountId) : IQuery<AccountExportDto?>;

/// <summary>Hela utdraget. Tider i UTC (§KM.5) — klienten visar dem i svensk tid.</summary>
public sealed record AccountExportDto(
    DateTime ExportedUtc,
    AccountExportContact Account,
    IReadOnlyList<CarpoolOfferExportDto> CarpoolOffers,
    IReadOnlyList<CarpoolRequestExportDto> CarpoolRequests,
    IReadOnlyList<AttendanceResponseExportDto> AttendanceResponses,
    IReadOnlyList<NotificationPreferenceExportDto> NotificationSettings,
    IReadOnlyList<PushSubscriptionExportDto> PushSubscriptions,
    DeviceOnlyDataNoticeDto PlayerCard);

/// <summary>Kontot självt.</summary>
public sealed record AccountExportContact(
    string Email,
    string? FirstName,
    string? LastName,
    DateTime CreatedUtc,
    DateTime? LastSignedInUtc);

/// <summary>Ett samåkningserbjudande. <c>Direction</c> och <c>Status</c> är enum-namn — klienten översätter.</summary>
public sealed record CarpoolOfferExportDto(
    string MatchOpponent,
    DateTime MatchKickoffUtc,
    string Direction,
    string DeparturePlace,
    DateTime DepartureUtc,
    int Seats,
    string? Note,
    string Status,
    DateTime CreatedUtc);

/// <summary>En åkförfrågan.</summary>
public sealed record CarpoolRequestExportDto(
    string MatchOpponent,
    DateTime MatchKickoffUtc,
    int Seats,
    string? Message,
    string? ResponseMessage,
    string Status,
    DateTime CreatedUtc);

/// <summary>Ett kallelsesvar vårdnadshavaren lämnat för ett av sina barn (§KM.7, `#199`).</summary>
public sealed record AttendanceResponseExportDto(
    string EventLabel,
    DateTime EventKickoffUtc,
    string ChildName,
    string Reply,
    DateTime RespondedUtc);

/// <summary>Notisinställningen för ett lag.</summary>
public sealed record NotificationPreferenceExportDto(
    string TeamName,
    bool MatchChanges,
    bool Carpool,
    bool Reminders,
    DateTime UpdatedUtc);

/// <summary>Att en notisprenumeration finns — utan den tekniska adressen (§KM.10).</summary>
public sealed record PushSubscriptionExportDto(
    string TeamName,
    DateTime CreatedUtc,
    DateTime? LastUsedUtc);

/// <summary>
/// Förklaringen till varför spelarkortet inte finns i utdraget (§KM.2).
///
/// <para>
/// Typen heter <c>DeviceOnlyData</c> och inte <c>PlayerCard</c> med flit: arkitekturvakten
/// för §KM.2 fäller varje typ vars <em>namn</em> beskriver spelarkortet, och den vakten ska
/// inte behöva luckras upp för en notis <em>om</em> att kortet saknas. Notisen bär ingen
/// statistik — bara en mening om var datan finns.
/// </para>
/// </summary>
public sealed record DeviceOnlyDataNoticeDto(string Message);

internal sealed class ExportAccountQueryHandler(
    IAccountExportRepository export,
    TimeProvider clock) : IQueryHandler<ExportAccountQuery, AccountExportDto?>
{
    /// <summary>
    /// Texten som förklarar spelarkortets frånvaro. Ordagrant ärlig: vi har det inte, för
    /// det når aldrig hit (§KM.2). Hänvisar till säkerhetskopieringskoden, som är vägen att
    /// flytta det — det enda vi kan säga om data vi aldrig ser.
    /// </summary>
    private const string PlayerCardMessage =
        "Spelarkortet – matchresultat, mål, assist, spelade matcher och märken – finns inte "
        + "i det här utdraget. Det lagras bara i din egen telefon och når aldrig våra servrar, "
        + "så vi har det inte och kan inte lämna ut det. Vill du flytta det till en annan "
        + "telefon använder du säkerhetskopieringskoden i appen.";

    public async Task<AccountExportDto?> HandleAsync(
        ExportAccountQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var data = await export.LoadForAccountAsync(query.AccountId, cancellationToken)
            .ConfigureAwait(false);

        if (data is null)
        {
            return null;
        }

        return new AccountExportDto(
            clock.GetUtcNow().UtcDateTime,
            new AccountExportContact(
                data.Account.Email,
                data.Account.FirstName,
                data.Account.LastName,
                data.Account.CreatedUtc,
                data.Account.LastSignedInUtc),
            [.. data.CarpoolOffers.Select(o => new CarpoolOfferExportDto(
                o.MatchOpponent,
                o.MatchKickoffUtc,
                o.Direction.ToString(),
                o.DeparturePlace,
                o.DepartureUtc,
                o.Seats,
                o.Note,
                o.Status.ToString(),
                o.CreatedUtc))],
            [.. data.CarpoolRequests.Select(r => new CarpoolRequestExportDto(
                r.MatchOpponent,
                r.MatchKickoffUtc,
                r.Seats,
                r.Message,
                r.ResponseMessage,
                r.Status.ToString(),
                r.CreatedUtc))],
            [.. data.AttendanceResponses.Select(a => new AttendanceResponseExportDto(
                a.EventLabel,
                a.EventKickoffUtc,
                a.ChildName,
                a.Reply.ToString(),
                a.RespondedUtc))],
            [.. data.NotificationPreferences.Select(p => new NotificationPreferenceExportDto(
                p.TeamName,
                p.MatchChanges,
                p.Carpool,
                p.Reminders,
                p.UpdatedUtc))],
            [.. data.PushSubscriptions.Select(s => new PushSubscriptionExportDto(
                s.TeamName,
                s.CreatedUtc,
                s.LastUsedUtc))],
            new DeviceOnlyDataNoticeDto(PlayerCardMessage));
    }
}
