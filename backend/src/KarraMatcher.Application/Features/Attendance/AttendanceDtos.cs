using KarraMatcher.Domain.Attendance;

namespace KarraMatcher.Application.Features.Attendance;

/// <summary>
/// Ett konts eget svar, så som det visas för att fyllas i på nytt.
///
/// <para>
/// Inget barn, inget namn — bara status och antal. Den vuxna ser sitt eget svar; ingen ser
/// någon annans genom den här (§KM.1). Tränarens summering är en annan yta (`#58`).
/// </para>
/// </summary>
public sealed record AttendanceResponseDto(AttendanceStatus Status, int Count, DateTime UpdatedUtc)
{
    public static AttendanceResponseDto For(AttendanceResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new AttendanceResponseDto(response.Status, response.Count, response.UpdatedUtc);
    }
}

/// <summary>
/// Läget för en vuxen som öppnar en match: är kallelsen öppnad, när är avspark, och vad
/// svarade jag sist.
///
/// <para>
/// Ett svar som säger allt gränssnittet behöver för att veta om det ska visa knappen alls
/// (<see cref="CallOpen"/>), om svaret fortfarande går att ändra (<see cref="KickoffUtc"/>
/// mot nu), och vad som redan är ifyllt (<see cref="MyResponse"/>).
/// </para>
/// </summary>
public sealed record AttendanceStateDto(
    bool CallOpen,
    DateTime KickoffUtc,
    AttendanceResponseDto? MyResponse);

/// <summary>
/// Ett svar så som tränaren ser det i summeringen (`#58`).
///
/// <para>
/// Namnet är en <b>vuxens</b> (`#154`) — den som svarade — inte ett barns (§KM.1). Null när
/// kontot ännu inte fyllt i något namn; tränaren ser då bara antalet, inte vem.
/// </para>
/// </summary>
public sealed record AttendanceResponderDto(
    Guid Id,
    string? Name,
    AttendanceStatus Status,
    int Count);

/// <summary>
/// Tränarens summering för en match (`#58`, §KM.7).
///
/// <h3>Det som avgör om matchen går att spela</h3>
///
/// <para>
/// <see cref="ComingPeople"/> är summan av antalen från dem som svarat Kommer — alltså hur
/// många som faktiskt dyker upp, inte hur många familjer som svarat. Det är den siffran
/// tränaren räknar mot elva.
/// </para>
///
/// <h3>Vad som medvetet inte finns här</h3>
///
/// <para>
/// <b>Ingen lista över dem som <em>inte</em> svarat.</b> Den kräver en lista på vilka som
/// förväntas svara — en förälder↔lag-koppling — och den finns inte, med flit: servern
/// lagrar inget om vilka barn eller familjer som hör till ett lag (§KM.1, beslut
/// 2026-09-10). Kopplingen införs först i <c>#63</c>, och då hör "inte svarat" och
/// påminnelsen hemma. Summeringen räknar bara dem som faktiskt svarat.
/// </para>
/// </summary>
public sealed record AttendanceSummaryDto(
    int ComingPeople,
    int MaybePeople,
    int CantComeFamilies,
    int RespondedFamilies,
    IReadOnlyList<AttendanceResponderDto> Responders);
