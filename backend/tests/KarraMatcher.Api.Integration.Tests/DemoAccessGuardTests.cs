using KarraMatcher.Api.Features.Auth;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Demo-grinden vid uppstart (`#611`, `#645`). Den fasta koden (424242) får finnas i utveckling
/// och i en uttrycklig staging-miljö, men aldrig i Production — där loggar riktiga familjer in.
/// En osatt miljö defaultar till Production, så en kvarglömd flagga faller fortfarande uppstarten.
/// </summary>
public sealed class DemoAccessGuardTests
{
    [Fact]
    public void PaslagenIProduction_FallerUppstarten()
    {
        var ex = Record.Exception(() =>
            DemoAccessGuard.EnsureNotInProduction(demoEnabled: true, Env("Production")));

        Assert.IsType<InvalidOperationException>(ex);
    }

    [Fact]
    public void PaslagenIStaging_TillatsForSandladan()
    {
        // En isolerad staging-miljö ska kunna köra demon utan att röra pilotens data.
        DemoAccessGuard.EnsureNotInProduction(demoEnabled: true, Env("Staging"));
    }

    [Fact]
    public void PaslagenIDevelopment_Tillats()
    {
        DemoAccessGuard.EnsureNotInProduction(demoEnabled: true, Env("Development"));
    }

    [Fact]
    public void AvslagenIProduction_FallerInte()
    {
        // Prod med demon av är det normala — grinden får aldrig stå i vägen för en riktig deploy.
        DemoAccessGuard.EnsureNotInProduction(demoEnabled: false, Env("Production"));
    }

    private static FakeHostEnvironment Env(string name) =>
        new() { EnvironmentName = name };

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "KarraMatcher.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
