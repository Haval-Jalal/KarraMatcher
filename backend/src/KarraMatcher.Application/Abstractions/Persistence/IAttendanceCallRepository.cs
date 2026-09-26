using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Händelsens sammanhang som en kallelse behöver: trupp, lag, avspark och typ (`#199`, `#289`).
/// Typen låter servern avvisa en kallelse för en övrig händelse (§KM.7) — samma gräns som FE
/// visar, men här som den riktiga grinden.
/// </summary>
public sealed record EventContext(Guid AgeGroupId, Guid TeamId, DateTime KickoffUtc, EventType Type);

/// <summary>Ett kallat barn med namn, lag och svar — för tränarens summering (`#199`).</summary>
public sealed record InvitationRow(
    Guid ChildId,
    string FirstName,
    string LastInitial,
    string? TeamName,
    string? ColorHex,
    AttendanceReply? Reply);

/// <summary>Den inloggades eget kallade barn och dess svar (`#199`).</summary>
public sealed record MyInvitationRow(
    Guid ChildId,
    string FirstName,
    string LastInitial,
    AttendanceReply? Reply);

/// <summary>Den inloggades eget barn i en cups trupp, och om det redan är anmält (`#296`).</summary>
public sealed record MyCupChildRow(Guid ChildId, string FirstName, string LastInitial, bool SignedUp);

/// <summary>En cup i truppen med sitt anmälningsläge — för trupp-vid cup-lista (`#304`).</summary>
public sealed record TruppCupRow(
    Guid EventId,
    string? Title,
    DateTime KickoffUtc,
    string TeamName,
    string ColorHex,
    int? Capacity,
    int ComingCount);

/// <summary>
/// Läser och skriver kallelsen (per barn) och dess svar (§KM.7, `#199`).
///
/// <para>
/// Kallelsen riktas mot utvalda barn ur hela truppen — inte bara händelsens eget lag —
/// eftersom ett lag fylls på med barn från andra lag vid behov. Svaren är per barn: Ja/Nej.
/// </para>
/// </summary>
public interface IAttendanceCallRepository
{
    /// <summary>Händelsens trupp, lag och avspark, eller null när händelsen inte finns.</summary>
    public Task<EventContext?> FindEventContextAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Händelsens avspark i UTC, eller null när den inte finns.</summary>
    public Task<DateTime?> FindKickoffUtcAsync(Guid eventId, CancellationToken cancellationToken);

    /// <summary>Kallelsen för händelsen, spårad. Null när den inte öppnats än.</summary>
    public Task<AttendanceCall?> FindCallByEventAsync(Guid eventId, CancellationToken cancellationToken);

    public Task AddCallAsync(AttendanceCall attendanceCall, CancellationToken cancellationToken);

    /// <summary>Alla barn-id som hör till truppen — för att pröva att ett urval är giltigt.</summary>
    public Task<IReadOnlySet<Guid>> ChildIdsInTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Kallelsens inbjudningar, spårade — för att synka urvalet.</summary>
    public Task<IReadOnlyList<AttendanceInvitation>> ListInvitationsAsync(
        Guid callId, CancellationToken cancellationToken);

    public Task AddInvitationAsync(AttendanceInvitation invitation, CancellationToken cancellationToken);

    public void RemoveInvitation(AttendanceInvitation invitation);

    /// <summary>Antal barn med Ja i kallelsen — cupens platsräkning, "fullt" räknas fram (`#295`).</summary>
    public Task<int> CountComingAsync(Guid callId, CancellationToken cancellationToken);

    /// <summary>En inbjudan (kallat barn), spårad — för att sätta ett svar. Null när barnet inte kallats.</summary>
    public Task<AttendanceInvitation?> FindInvitationAsync(
        Guid callId, Guid childId, CancellationToken cancellationToken);

    /// <summary>De kallade barnen med namn, lag och svar (tränarens summering).</summary>
    public Task<IReadOnlyList<InvitationRow>> ListInvitationRowsAsync(
        Guid callId, CancellationToken cancellationToken);

    /// <summary>Den inloggades egna kallade barn för händelsen (vårdnadshavarens vy).</summary>
    public Task<IReadOnlyList<MyInvitationRow>> ListMineAsync(
        Guid eventId, Guid accountId, CancellationToken cancellationToken);

    /// <summary>
    /// Den inloggades egna barn i truppen, och om vart och ett är anmält till kallelsen (`#296`).
    /// För cupens öppna anmälan: vårdnadshavaren ser sina barn (även oanmälda) att anmäla. Ett
    /// tomt <paramref name="callId"/> (<see cref="Guid.Empty"/>) betyder att ingen är anmäld än.
    /// </summary>
    public Task<IReadOnlyList<MyCupChildRow>> MyCupChildrenAsync(
        Guid ageGroupId, Guid accountId, Guid callId, CancellationToken cancellationToken);

    /// <summary>Truppens cuper med anmälningsläge, i avsparksordning — för cup-listan (`#304`).</summary>
    public Task<IReadOnlyList<TruppCupRow>> ListTruppCupsAsync(
        Guid ageGroupId, CancellationToken cancellationToken);

    /// <summary>Är kontot vårdnadshavare för barnet?</summary>
    public Task<bool> IsGuardianOfChildAsync(
        Guid accountId, Guid childId, CancellationToken cancellationToken);

    /// <summary>Vårdnadshavarnas konto-id för en uppsättning barn — för riktad notis.</summary>
    public Task<IReadOnlyList<Guid>> GuardianAccountIdsForChildrenAsync(
        IReadOnlyCollection<Guid> childIds, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
