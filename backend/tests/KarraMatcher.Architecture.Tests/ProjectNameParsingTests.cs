namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// Regressionstest för en bugg som bara syntes i CI: csproj skriver
/// projektreferenser med omvänt snedstreck, vilket Windows tolkar som separator
/// men Linux inte gör. Testerna nedan är plattformsoberoende och skulle ha
/// fångat det direkt på utvecklarmaskinen.
/// </summary>
public class ProjectNameParsingTests
{
    [Theory]
    [InlineData(@"..\KarraMatcher.Domain\KarraMatcher.Domain.csproj", "KarraMatcher.Domain")]
    [InlineData("../KarraMatcher.Domain/KarraMatcher.Domain.csproj", "KarraMatcher.Domain")]
    [InlineData(@"..\A\B\KarraMatcher.Application.csproj", "KarraMatcher.Application")]
    [InlineData("KarraMatcher.Api.csproj", "KarraMatcher.Api")]
    public void ProjectNameFrom_OavsettSeparator_GerProjektnamnet(string include, string expected)
    {
        Assert.Equal(expected, ProjectReferenceTests.ProjectNameFrom(include));
    }

    [Fact]
    public void ProjectNameFrom_TomStrang_GerTomStrang()
    {
        Assert.Equal(string.Empty, ProjectReferenceTests.ProjectNameFrom(string.Empty));
    }
}
