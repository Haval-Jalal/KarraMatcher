using KarraMatcher.Domain.Common;

namespace KarraMatcher.Domain.Tests;

/// <summary>
/// Domänlagret är tomt tills första entiteten kommer i #6. Testet håller
/// projektet levande och bevakar att markören förblir utan medlemmar —
/// den är ett ankare för assembly-scanning, inte en basklass.
/// </summary>
public class DomainAssemblyTests
{
    [Fact]
    public void DomainMarker_ArTomOchAnvandsBaraSomAnkare()
    {
        Assert.True(typeof(IDomainMarker).IsInterface);
        Assert.Empty(typeof(IDomainMarker).GetMembers());
    }
}
