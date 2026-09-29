using KarraMatcher.Application.Features.Chat;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// En chatt-reaktion är ett val ur ett fast urval, inte fritext (`#402`). Servern måste vara minst
/// lika strikt som klienten — annars kunde ett riktat anrop spara en godtycklig sträng som reaktion.
/// </summary>
public class ToggleReactionCommandValidatorTests
{
    private readonly ToggleReactionCommandValidator _validator = new();

    private static ToggleReactionCommand With(string emoji) =>
        new(Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(), emoji);

    [Theory]
    [InlineData("👍")]
    [InlineData("❤️")]
    [InlineData("😂")]
    [InlineData("😮")]
    [InlineData("😢")]
    [InlineData("👏")]
    public void TillatenEmoji_ArGodkand(string emoji)
    {
        Assert.True(_validator.Validate(With(emoji)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("🚀")] // en emoji utanför urvalet
    [InlineData("👍👍")] // dubblerad
    [InlineData("inte en emoji utan en lång sträng som någon försöker smuggla in")]
    public void OtillatenEllerLangSträng_ArOgiltig(string emoji)
    {
        Assert.False(_validator.Validate(With(emoji)).IsValid);
    }
}
