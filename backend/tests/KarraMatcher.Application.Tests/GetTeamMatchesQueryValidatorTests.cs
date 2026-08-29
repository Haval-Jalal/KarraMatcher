using KarraMatcher.Application.Features.Teams.GetTeamMatches;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Sluggen kommer fran URL:en och ar darmed anvandarindata. Validatorn ar det som gor att
/// skrap avvisas med 400 innan det nar databasen.
/// </summary>
public class GetTeamMatchesQueryValidatorTests
{
    private readonly GetTeamMatchesQueryValidator _validator = new();

    [Theory]
    [InlineData("gul")]
    [InlineData("bla")]
    [InlineData("p2016-gul")]
    [InlineData("lag-2")]
    public void Validate_GiltigSlug_ArGodkand(string slug)
    {
        Assert.True(_validator.Validate(new GetTeamMatchesQuery(slug)).IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Gul")]              // versaler
    [InlineData("gul lag")]          // mellanslag
    [InlineData("gul/../admin")]     // sokvagsmanipulation
    [InlineData("gul' or 1=1--")]    // injektionsforsok
    [InlineData("gul%00")]
    [InlineData("gott-lag-ä")]       // svenska tecken hor inte hemma i en slug (KM.9)
    public void Validate_OgiltigSlug_ArUnderkand(string slug)
    {
        Assert.False(_validator.Validate(new GetTeamMatchesQuery(slug)).IsValid);
    }

    [Fact]
    public void Validate_ForLangSlug_ArUnderkand()
    {
        Assert.False(_validator.Validate(new GetTeamMatchesQuery(new string('a', 81))).IsValid);
    }
}
