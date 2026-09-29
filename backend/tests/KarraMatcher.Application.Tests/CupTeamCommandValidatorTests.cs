using KarraMatcher.Application.Features.Cup;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Cup-lags-kommandona bär bara id:n, men saknade tidigare validator helt så att
/// CommandValidationBehavior hoppade tyst över dem (`#403`). Nu avvisas ett tomt Guid.
/// </summary>
public class CupTeamCommandValidatorTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Fact]
    public void DeleteCupTeam_MedIder_ArGiltigt()
    {
        var result = new DeleteCupTeamCommandValidator().Validate(
            new DeleteCupTeamCommand(Id, Id, Id));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void DeleteCupTeam_UtanCupTeamId_ArOgiltigt()
    {
        var result = new DeleteCupTeamCommandValidator().Validate(
            new DeleteCupTeamCommand(Id, Id, Guid.Empty));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void AssignCupChild_MedIder_ArGiltigt()
    {
        var result = new AssignCupChildCommandValidator().Validate(
            new AssignCupChildCommand(Id, Id, Id, Id));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void AssignCupChild_UtanBarn_ArOgiltigt()
    {
        var result = new AssignCupChildCommandValidator().Validate(
            new AssignCupChildCommand(Id, Id, Id, Guid.Empty));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void UnassignCupChild_UtanBarn_ArOgiltigt()
    {
        var result = new UnassignCupChildCommandValidator().Validate(
            new UnassignCupChildCommand(Id, Id, Id, Guid.Empty));

        Assert.False(result.IsValid);
    }
}
