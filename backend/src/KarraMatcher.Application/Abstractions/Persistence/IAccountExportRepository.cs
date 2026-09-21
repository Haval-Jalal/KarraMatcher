using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser allt servern äger om ett konto, för registerutdraget (`#67`, checklistan 9.13).
///
/// <para>
/// Ett eget läsförråd i stället för en ny "lista per konto"-metod på vart och ett av de
/// fem repona. Skälet är dels att hålla joinsen mot match och lag på ett ställe, dels att
/// push-prenumerationens repo <em>med flit</em> aldrig lämnar ut endpoint eller nycklar
/// (§KM.10) — det förbudet ska inte behöva luckras upp för att kunna beskriva att en
/// prenumeration finns. Projektionen här väljer aldrig secreten.
/// </para>
/// </summary>
public interface IAccountExportRepository
{
    /// <summary>
    /// Allt som hör till kontot, eller <c>null</c> om kontot inte finns. Tomma listor när
    /// kontot finns men inte gjort något — inte null.
    /// </summary>
    public Task<AccountExportData?> LoadForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken);
}

/// <summary>Allt servern har om ett konto, samlat. Läsmodeller — inga entiteter, inga secrets.</summary>
public sealed record AccountExportData(
    AccountExportRow Account,
    IReadOnlyList<CarpoolOfferExportRow> CarpoolOffers,
    IReadOnlyList<CarpoolRequestExportRow> CarpoolRequests,
    IReadOnlyList<AttendanceResponseExportRow> AttendanceResponses,
    IReadOnlyList<NotificationPreferenceExportRow> NotificationPreferences,
    IReadOnlyList<PushSubscriptionExportRow> PushSubscriptions);

/// <summary>Kontot självt. En vuxens egna uppgifter (§KM.1) — aldrig något om ett barn.</summary>
public sealed record AccountExportRow(
    string Email,
    string? FirstName,
    string? LastName,
    DateTime CreatedUtc,
    DateTime? LastSignedInUtc);

/// <summary>Ett samåkningserbjudande föräldern lagt upp, med matchen den gäller.</summary>
public sealed record CarpoolOfferExportRow(
    string MatchOpponent,
    DateTime MatchKickoffUtc,
    CarpoolDirection Direction,
    string DeparturePlace,
    DateTime DepartureUtc,
    int Seats,
    string? Note,
    CarpoolOfferStatus Status,
    DateTime CreatedUtc);

/// <summary>En åkförfrågan föräldern skickat, med matchen den gäller.</summary>
public sealed record CarpoolRequestExportRow(
    string MatchOpponent,
    DateTime MatchKickoffUtc,
    int Seats,
    string? Message,
    string? ResponseMessage,
    CarpoolRequestStatus Status,
    DateTime CreatedUtc);

/// <summary>Ett kallelsesvar vårdnadshavaren lämnat för ett av sina barn (§KM.7, `#199`).</summary>
public sealed record AttendanceResponseExportRow(
    string EventLabel,
    DateTime EventKickoffUtc,
    string ChildName,
    AttendanceReply Reply,
    DateTime RespondedUtc);

/// <summary>Notisinställningen för ett lag.</summary>
public sealed record NotificationPreferenceExportRow(
    string TeamName,
    bool MatchChanges,
    bool Carpool,
    bool Reminders,
    DateTime UpdatedUtc);

/// <summary>
/// Att en prenumeration finns för ett lag — <b>utan</b> den tekniska adressen och nycklarna.
/// De är secrets och lämnar aldrig servern (§KM.10).
/// </summary>
public sealed record PushSubscriptionExportRow(
    string TeamName,
    DateTime CreatedUtc,
    DateTime? LastUsedUtc);
