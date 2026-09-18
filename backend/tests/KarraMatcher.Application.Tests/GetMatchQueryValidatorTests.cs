using KarraMatcher.Application.Features.Events.GetEvent;

namespace KarraMatcher.Application.Tests;

public class GetEventQueryValidatorTests
{
    private readonly GetEventQueryValidator _validator = new();

    [Fact]
    public void Validate_TomtId_ArUnderkant()
    {
        Assert.False(_validator.Validate(new GetEventQuery(Guid.Empty)).IsValid);
    }

    [Fact]
    public void Validate_RiktigtId_ArGodkant()
    {
        Assert.True(_validator.Validate(new GetEventQuery(Guid.NewGuid())).IsValid);
    }
}
