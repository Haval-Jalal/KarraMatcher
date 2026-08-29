using System.Reflection;
using KarraMatcher.Domain.Common;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// Bevakar Clean Architecture-gränserna. Beroenden ska alltid peka inåt:
/// Api → Infrastructure → Application → Domain.
///
/// Testerna läser assembly-referenser direkt, utan extra bibliotek. De rikare
/// NetArchTest-reglerna (namnkonventioner, entiteter i controllers) kommer i #10.
/// </summary>
public class LayerBoundaryTests
{
    private static readonly Assembly Domain = typeof(IDomainMarker).Assembly;
    private static readonly Assembly Application = typeof(KarraMatcher.Application.DependencyInjection).Assembly;
    private static readonly Assembly Infrastructure = typeof(KarraMatcher.Infrastructure.DependencyInjection).Assembly;

    private static string[] ReferencesOf(Assembly assembly) =>
        [.. assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty)];

    [Theory]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("FluentValidation")]
    [InlineData("Npgsql")]
    public void Domain_HarIngaRamverksberoenden(string forbiddenPrefix)
    {
        var offenders = ReferencesOf(Domain)
            .Where(name => name.StartsWith(forbiddenPrefix, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Domain refererar {string.Join(", ", offenders)}. Domänlagret ska ha noll "
            + "ramverksberoenden — se CLAUDE.md → Backend, Arkitektur & struktur.");
    }

    [Fact]
    public void Domain_RefererarInteApplication()
    {
        Assert.DoesNotContain("KarraMatcher.Application", ReferencesOf(Domain));
    }

    [Fact]
    public void Application_RefererarInteInfrastructure()
    {
        Assert.DoesNotContain("KarraMatcher.Infrastructure", ReferencesOf(Application));
    }

    [Fact]
    public void Application_RefererarInteApi()
    {
        Assert.DoesNotContain("KarraMatcher.Api", ReferencesOf(Application));
    }

    [Fact]
    public void Infrastructure_RefererarInteApi()
    {
        Assert.DoesNotContain("KarraMatcher.Api", ReferencesOf(Infrastructure));
    }

    // Ingen positiv assertion här: kompilatorn tar bort assembly-referenser som ingen
    // kod använder, så "Application refererar Domain" kan inte bevisas på den här nivån
    // förrän Domain har typer. Den riktningen kontrolleras i ProjectReferenceTests.
}
