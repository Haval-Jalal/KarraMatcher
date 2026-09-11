using KarraMatcher.Application.Features.Attendance;
using KarraMatcher.Domain.Attendance;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Reglerna för ett närvarosvar (`#57`, §KM.7).
///
/// <para>
/// Antalet prövas server-side, inte bara i formuläret. "Kommer" utan någon som kommer är
/// inget svar; "kan inte" och "kanske" får vara noll.
/// </para>
/// </summary>
public class AttendanceResponseValidatorTests
{
    private readonly SubmitAttendanceResponseCommandValidator _validator = new();

    private static SubmitAttendanceResponseCommand Command(AttendanceStatus status, int count) =>
        new(Guid.NewGuid(), Guid.NewGuid(), status, count);

    [Theory]
    [InlineData(AttendanceStatus.Coming, 1)]
    [InlineData(AttendanceStatus.Coming, 4)]
    [InlineData(AttendanceStatus.CantCome, 0)]
    [InlineData(AttendanceStatus.CantCome, 3)]
    [InlineData(AttendanceStatus.Maybe, 0)]
    [InlineData(AttendanceStatus.Maybe, 2)]
    public void Validate_RimligtSvar_ArGodkant(AttendanceStatus status, int count)
    {
        Assert.True(_validator.Validate(Command(status, count)).IsValid);
    }

    [Fact]
    public void Validate_KommerUtanAntal_ArUnderkant()
    {
        // Ett "kommer" utan nagon som kommer sager ingenting -- bara "kommer" kraver minst en.
        Assert.False(_validator.Validate(Command(AttendanceStatus.Coming, 0)).IsValid);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-1)]
    public void Validate_AntalUtanforGransen_ArUnderkant(int count)
    {
        Assert.False(_validator.Validate(Command(AttendanceStatus.Maybe, count)).IsValid);
    }

    [Fact]
    public void Validate_OkandStatus_ArUnderkant()
    {
        // Ett varde utanfor enumen kan inte komma fran gransnittet, men en klient som skickar
        // ratt att slippa (int)7 ska motas av 400, inte tyst sparas som nagot.
        Assert.False(_validator.Validate(Command((AttendanceStatus)7, 1)).IsValid);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Validate_TomtId_ArUnderkant(bool emptyMatch, bool emptyAccount)
    {
        var command = new SubmitAttendanceResponseCommand(
            emptyMatch ? Guid.Empty : Guid.NewGuid(),
            emptyAccount ? Guid.Empty : Guid.NewGuid(),
            AttendanceStatus.Coming,
            1);

        Assert.False(_validator.Validate(command).IsValid);
    }
}
