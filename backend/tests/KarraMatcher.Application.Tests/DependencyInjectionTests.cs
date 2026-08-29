using KarraMatcher.Application;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ByggerEnGiltigServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddApplication();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.NotNull(provider);
    }
}
