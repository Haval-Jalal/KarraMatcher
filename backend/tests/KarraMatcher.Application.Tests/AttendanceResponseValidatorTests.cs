using KarraMatcher.Application.Features.Attendance;
using KarraMatcher.Domain.Attendance;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Reglerna för den riktade kallelsen (§KM.7, `#199`): admin skickar ett urval barn, och en
/// vårdnadshavare svarar Ja/Nej per barn.
/// </summary>
public class AttendanceResponseValidatorTests
{
    private readonly SetKallelseCommandValidator _set = new();
    private readonly RespondToKallelseCommandValidator _respond = new();

    [Fact]
    public void Set_MedGiltigtUrval_ArGodkant()
    {
        var command = new SetKallelseCommand(
            Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()], Guid.NewGuid());

        Assert.True(_set.Validate(command).IsValid);
    }

    [Fact]
    public void Set_TomtUrval_ArGodkant()
    {
        // Ett tomt urval ar giltigt -- det tomma en kallelse eller kallar ingen an.
        var command = new SetKallelseCommand(Guid.NewGuid(), Guid.NewGuid(), [], Guid.NewGuid());

        Assert.True(_set.Validate(command).IsValid);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Set_TomtId_ArUnderkant(bool emptyTrupp, bool emptyEvent, bool emptyActor)
    {
        var command = new SetKallelseCommand(
            emptyTrupp ? Guid.Empty : Guid.NewGuid(),
            emptyEvent ? Guid.Empty : Guid.NewGuid(),
            [Guid.NewGuid()],
            emptyActor ? Guid.Empty : Guid.NewGuid());

        Assert.False(_set.Validate(command).IsValid);
    }

    [Theory]
    [InlineData(AttendanceReply.Coming)]
    [InlineData(AttendanceReply.NotComing)]
    public void Respond_MedGiltigtSvar_ArGodkant(AttendanceReply reply)
    {
        var command = new RespondToKallelseCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), reply);

        Assert.True(_respond.Validate(command).IsValid);
    }

    [Fact]
    public void Respond_OkantSvar_ArUnderkant()
    {
        // Ett varde utanfor enumen kan inte komma fran gransnittet, men en klient som skickar
        // (int)7 ska motas av 400, inte tyst sparas som nagot.
        var command = new RespondToKallelseCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (AttendanceReply)7);

        Assert.False(_respond.Validate(command).IsValid);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Respond_TomtId_ArUnderkant(bool emptyEvent, bool emptyChild, bool emptyAccount)
    {
        var command = new RespondToKallelseCommand(
            emptyEvent ? Guid.Empty : Guid.NewGuid(),
            emptyChild ? Guid.Empty : Guid.NewGuid(),
            emptyAccount ? Guid.Empty : Guid.NewGuid(),
            AttendanceReply.Coming);

        Assert.False(_respond.Validate(command).IsValid);
    }
}
