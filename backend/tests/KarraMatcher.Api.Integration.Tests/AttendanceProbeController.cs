using KarraMatcher.Api.Features.Attendance;

using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// En endpoint som finns bara för att pröva grinden (`#56`, §KM.7).
///
/// <para>
/// Den ligger i testprojektet och kopplas in med <see cref="TestApp.WithAttendanceProbe"/>,
/// så den kan inte råka följa med till drift. Att pröva grinden mot en riktig endpoint hade
/// varit bättre — men kallelsens endpoints byggs i <c>#57</c>, och en grind som ingen provat
/// upptäcker man är trasig samma dag som funktionen släpps.
/// </para>
///
/// <para>
/// Två routes med flit: den ena bär laget som slug, den andra som match. Det är de två
/// formerna kallelsens riktiga adresser kommer att ha, och grinden ska klara båda.
/// </para>
/// </summary>
[ApiController]
[RequireAttendanceEnabled]
public sealed class AttendanceProbeController : ControllerBase
{
    [HttpGet("test/attendance/team/{slug}")]
    public IActionResult ByTeam(string slug) => Ok(new { slug });

    [HttpGet("test/attendance/match/{matchId:guid}")]
    public IActionResult ByMatch(Guid matchId) => Ok(new { matchId });

    /// <summary>En route som inte bär något lag alls. Grinden ska stänga den.</summary>
    [HttpGet("test/attendance/utan-lag")]
    public IActionResult WithoutTeam() => Ok(new { ok = true });
}
