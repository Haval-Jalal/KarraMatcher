using KarraMatcher.Application.Features.Chat;

namespace KarraMatcher.Application.Tests;

public class GetTeamChatMetaQueryValidatorTests
{
    private readonly GetTeamChatMetaQueryValidator _validator = new();

    [Fact]
    public void Validate_TomSlug_ArUnderkant()
    {
        Assert.False(_validator.Validate(new GetTeamChatMetaQuery("", Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void Validate_TomtKonto_ArUnderkant()
    {
        Assert.False(_validator.Validate(new GetTeamChatMetaQuery("gul", Guid.Empty)).IsValid);
    }

    [Fact]
    public void Validate_SlugOchKonto_ArGodkant()
    {
        Assert.True(_validator.Validate(new GetTeamChatMetaQuery("gul", Guid.NewGuid())).IsValid);
    }
}
