using KarraMatcher.Application.Features.Children;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Validatorerna för de slug-bärande roster-/ref-queryerna (#586). Utan dem hoppas valideringen
/// tyst över (samma klass som #551); här vaktas att de kräver en giltig slug-grammatik.
/// </summary>
public class TeamSlugQueryValidatorTests
{
    private readonly GetTeamRosterQueryValidator _roster = new();
    private readonly GetTeamRefBySlugQueryValidator _ref = new();

    [Theory]
    [InlineData("")]
    [InlineData("Gul")] // versaler
    [InlineData("gul lag")] // mellanslag
    [InlineData("gul/../bla")] // path-traversal-tecken
    public void OgiltigSlug_ArUnderkant(string slug)
    {
        Assert.False(_roster.Validate(new GetTeamRosterQuery(slug)).IsValid);
        Assert.False(_ref.Validate(new GetTeamRefBySlugQuery(slug)).IsValid);
    }

    [Fact]
    public void GiltigSlug_ArGodkand()
    {
        Assert.True(_roster.Validate(new GetTeamRosterQuery("gul-2016")).IsValid);
        Assert.True(_ref.Validate(new GetTeamRefBySlugQuery("gul-2016")).IsValid);
    }
}
