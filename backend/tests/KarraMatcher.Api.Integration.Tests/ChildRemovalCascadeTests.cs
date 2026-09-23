using KarraMatcher.Domain.Children;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Vakten för att ett barn som tas bort ur en trupp tar sina rader med sig (§KM.6, `#203`).
/// Samma idé som <c>AlltSomPekarPaEttKonto_ForsvinnerMedDet</c> för konton: i stället för att
/// lita på att någon minns det räknar testet upp varje främmande nyckel mot <see cref="Child"/>
/// och kräver kaskad. Den som lägger till en tabell som pekar på ett barn utan att den
/// försvinner med barnet får reda på det direkt.
/// </summary>
public sealed class ChildRemovalCascadeTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    [Fact]
    public void AlltSomPekarPaEttBarn_ForsvinnerMedDet()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var offenders = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetForeignKeys())
            .Where(fk => fk.PrincipalEntityType.ClrType == typeof(Child))
            .Where(fk => fk.DeleteBehavior != DeleteBehavior.Cascade)
            .Select(fk => $"{fk.DeclaringEntityType.ClrType.Name}.{fk.Properties[0].Name}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            "Följande pekar på ett barn utan att försvinna med det (§KM.6): "
                + string.Join(", ", offenders));
    }
}
