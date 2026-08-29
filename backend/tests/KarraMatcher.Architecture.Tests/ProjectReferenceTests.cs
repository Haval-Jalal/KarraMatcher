using System.Reflection;
using System.Xml.Linq;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// Kontrollerar de <em>deklarerade</em> projektreferenserna i csproj-filerna.
///
/// Varför inte assembly-referenser? Kompilatorn tar bort referenser som ingen kod
/// använder, så ett projekt kan deklarera en otillåten referens utan att den syns i
/// den byggda assemblyn. Clean Architecture handlar om vad lagren <em>får</em> bero på,
/// inte om vad kompilatorn råkade behålla — därför läser vi källan.
/// </summary>
public class ProjectReferenceTests
{
    private static readonly string BackendRoot =
        Assembly.GetExecutingAssembly()
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "BackendRoot").Value!;

    private static XDocument Load(string project)
    {
        var path = Path.Combine(BackendRoot, "src", project, project + ".csproj");
        Assert.True(File.Exists(path), $"Hittade inte {path}");
        return XDocument.Load(path);
    }

    /// <summary>
    /// Plockar projektnamnet ur ett Include-attribut.
    ///
    /// MSBuild skriver sökvägar med omvänt snedstreck oavsett plattform. Windows
    /// behandlar det som separator, Linux gör det inte — så
    /// <c>Path.GetFileNameWithoutExtension</c> direkt på råvärdet ger rätt svar på
    /// utvecklarmaskinen och fel i CI. Vi normaliserar därför först.
    /// </summary>
    internal static string ProjectNameFrom(string include) =>
        Path.GetFileNameWithoutExtension(include.Replace(@"\", "/", StringComparison.Ordinal));

    private static string[] DeclaredReferences(string project) =>
        [.. Load(project).Descendants("ProjectReference")
            .Select(e => ProjectNameFrom((string?)e.Attribute("Include") ?? string.Empty))];

    private static string[] DeclaredPackages(string project) =>
        [.. Load(project).Descendants("PackageReference")
            .Select(e => (string?)e.Attribute("Include") ?? string.Empty)];

    [Fact]
    public void Domain_DeklarerarIngaProjektreferenser()
    {
        Assert.Empty(DeclaredReferences("KarraMatcher.Domain"));
    }

    [Fact]
    public void Application_DeklarerarBaraDomain()
    {
        Assert.Equal(["KarraMatcher.Domain"], DeclaredReferences("KarraMatcher.Application"));
    }

    [Fact]
    public void Infrastructure_DeklarerarBaraApplication()
    {
        Assert.Equal(["KarraMatcher.Application"], DeclaredReferences("KarraMatcher.Infrastructure"));
    }

    [Fact]
    public void Domain_DeklarerarIngaNuGetPaket()
    {
        // Domänlagret ska ha noll ramverksberoenden. Vi förbjuder alla paket i stället
        // för att lista otillåtna — då tvingar varje undantag fram ett medvetet beslut.
        //
        // Den här kontrollen läser csproj:en, inte den byggda assemblyn. Ett oanvänt
        // paket elideras av kompilatorn och skulle annars slinka igenom obemärkt.
        var packages = DeclaredPackages("KarraMatcher.Domain");

        Assert.True(
            packages.Length == 0,
            $"Domain deklarerar {string.Join(", ", packages)}. Domänlagret ska ha noll "
            + "ramverksberoenden — se CLAUDE.md → Backend, Arkitektur & struktur.");
    }

    [Fact]
    public void Api_DeklarerarInteDomainDirekt()
    {
        // Api får nå Domain via Application, men ska inte hoppa över lagren.
        Assert.DoesNotContain("KarraMatcher.Domain", DeclaredReferences("KarraMatcher.Api"));
    }
}
