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
