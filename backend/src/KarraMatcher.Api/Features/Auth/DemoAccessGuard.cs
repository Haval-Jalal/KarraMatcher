namespace KarraMatcher.Api.Features.Auth;

/// <summary>
/// Vaktar demo-åtkomsten (demo-seed + den fasta koden <c>424242</c>) vid uppstart (`#611`, `#645`).
///
/// <para>
/// Demokontona loggar in med en fast kod i stället för en mejlad — oumbärligt för test, livsfarligt
/// bredvid riktiga familjer. Därför får <c>DemoSeed:Enabled</c> finnas i utveckling och i en
/// uttrycklig <b>staging</b>-miljö (en isolerad sandlåda med egen databas), men <b>aldrig</b> i
/// Production.
/// </para>
///
/// <para>
/// Grinden mäter på <see cref="IHostEnvironment.IsProduction"/>, inte på "är inte utveckling":
/// en osatt <c>ASPNETCORE_ENVIRONMENT</c> defaultar till Production, så en kvarglömd flagga faller
/// fortfarande uppstarten (Render behåller då den gamla containern — ett tyst men säkert utfall).
/// Att öppna demon kräver två medvetna val: miljön satt till något annat än Production <em>och</em>
/// <c>DemoSeed:Enabled=true</c>. Det var den enda luckan `#611` lämnade: då blockerades även en
/// legitim staging-miljö, så att testa en driftsatt ändring utan att röra pilotens data gick inte.
/// </para>
/// </summary>
internal static class DemoAccessGuard
{
    /// <summary>
    /// Kastar om demon är påslagen i Production. Säker att anropa oavsett flaggans läge.
    /// </summary>
    public static void EnsureNotInProduction(bool demoEnabled, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        if (demoEnabled && environment.IsProduction())
        {
            throw new InvalidOperationException(
                "DemoSeed:Enabled får aldrig vara på i Production. Demokontona loggar in med en fast "
                    + "kod och hör bara hemma i utveckling eller en isolerad staging-miljö. "
                    + "Stäng av den i prod, eller sätt ASPNETCORE_ENVIRONMENT=Staging för en sandlåda.");
        }
    }
}
