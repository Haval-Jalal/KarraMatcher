using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Blinda fläcken i kaskad-vakterna (§KM.6, `#583`). <c>AlltSomPekarPaEttKonto…</c> och
/// <c>AlltSomPekarPaEttBarn…</c> räknar bara upp <em>konfigurerade</em> främmande nycklar. En
/// referens lagrad som bar <see cref="Guid"/> utan relation är osynlig för dem — så en framtida
/// <c>X.ChildId</c>/<c>X.AccountId</c> utan FK skulle lagra barn-/konto-länkade rader som varken
/// kaskaderar eller fäller något test. Det här testet stänger luckan: varje Guid-egenskap som
/// <em>heter</em> som en konto-/barn-referens men saknar backande FK måste stå på en uttrycklig,
/// sanktionerad lista (annars är den en dold referens som inte försvinner med sitt principal).
/// </summary>
public sealed class PlainGuidReferenceGuardTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    /// <summary>
    /// Sanktionerade id-utan-FK mot ett konto: audit-noteringar som med flit överlever raderingen
    /// som ett namnlöst id (§KM.6/§KM.10). Att lägga till här är ett medvetet beslut.
    /// </summary>
    private static readonly HashSet<string> SanctionedAccountRefs =
    [
        "AuditEntry.ActorAccountId", // audit-loggen saknar FK med flit (§KM.10)
        "AttendanceCall.OpenedByAccountId", // §KM.6 namngivet undantag
        "AttendanceInvitation.RespondedByAccountId", // §KM.6 namngivet undantag
        "ChatMessage.DeletedByAccountId", // §KM.6 namngivet undantag
        "MembershipApplication.ResolvedByAccountId", // vem som avgjorde ansökan (audit-not, #583)
        "Invitation.CreatedByAccountId", // vem som skapade inbjudan (audit-not, #583)
    ];

    /// <summary>Inga sanktionerade id-utan-FK mot ett barn i dag — barn-referenser ska kaskadera.</summary>
    private static readonly HashSet<string> SanctionedChildRefs = [];

    [Fact]
    public void KontoreferensUtanFrammandeNyckel_ArSanktionerad()
    {
        AssertNoHiddenReferences("AccountId", SanctionedAccountRefs);
    }

    [Fact]
    public void BarnreferensUtanFrammandeNyckel_ArSanktionerad()
    {
        AssertNoHiddenReferences("ChildId", SanctionedChildRefs);
    }

    private void AssertNoHiddenReferences(string suffix, HashSet<string> sanctioned)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var offenders = context.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties().Select(property => (entity, property)))
            .Where(x =>
                (x.property.ClrType == typeof(Guid) || x.property.ClrType == typeof(Guid?))
                && x.property.Name.EndsWith(suffix, StringComparison.Ordinal))
            // Egenskaper som backar en FK täcks redan av kaskad-testerna; här jagar vi de utan FK.
            .Where(x => !x.entity.GetForeignKeys().Any(fk => fk.Properties.Contains(x.property)))
            .Select(x => $"{x.entity.ClrType.Name}.{x.property.Name}")
            .Where(name => !sanctioned.Contains(name))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Följande ser ut som en {suffix}-referens men saknar backande FK och står inte på den "
                + "sanktionerade listan (§KM.6, #583). Antingen: ge den en kaskad-FK, eller lägg den "
                + "som ett medvetet undantag (och i §KM.6): "
                + string.Join(", ", offenders));
    }
}
