using KarraMatcher.Application.Features.Matches.GetMatch;

namespace KarraMatcher.Application.Tests;

public class GetMatchQueryValidatorTests
{
    private readonly GetMatchQueryValidator _validator = new();

    [Fact]
    public void Validate_TomtId_ArUnderkant()
    {
        Assert.False(_validator.Validate(new GetMatchQuery(Guid.Empty)).IsValid);
    }

    [Fact]
    public void Validate_RiktigtId_ArGodkant()
    {
        Assert.True(_validator.Validate(new GetMatchQuery(Guid.NewGuid())).IsValid);
    }
}
