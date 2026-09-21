using KarraMatcher.Application.Features.Chat;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Validatorn för ett chattmeddelande (`#201`): en text krävs, och taket prövas server-side
/// (§KM.10 — inte bara i formuläret).
/// </summary>
public class PostChatMessageCommandValidatorTests
{
    private readonly PostChatMessageCommandValidator _validator = new();

    private static PostChatMessageCommand Command(string body) =>
        new(Guid.NewGuid(), TeamId: null, Guid.NewGuid(), body, null);

    [Fact]
    public void Validate_MedText_ArGodkand()
    {
        Assert.True(_validator.Validate(Command("Vem tar med bollar?")).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_UtanText_ArUnderkand(string body)
    {
        Assert.False(_validator.Validate(Command(body)).IsValid);
    }

    [Fact]
    public void Validate_ForLangText_ArUnderkand()
    {
        var tooLong = new string('a', PostChatMessageCommandValidator.MaxBody + 1);

        Assert.False(_validator.Validate(Command(tooLong)).IsValid);
    }
}
