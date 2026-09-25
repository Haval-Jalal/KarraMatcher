namespace KarraMatcher.Application.Features.Auth;

/// <summary>
/// Test-inloggning för demokonton (`#269`). De två demokontona (admin + förälder, seedade av
/// DemoSeed) loggar in med en <b>fast kod</b> i stället för en mejlad — så hela appen går att
/// prova utan en verifierad avsändardomän hos mejlleverantören.
///
/// <para>
/// <b>Bara för test. Stäng av före lansering</b> (<c>DemoSeed:Enabled=false</c>). Avstängt som
/// standard, och den fasta koden gäller <em>enbart</em> de två konfigurerade demoadresserna —
/// aldrig ett riktigt konto. Läses ur samma konfig-sektion som demo-seeden.
/// </para>
/// </summary>
public sealed class DemoAccessOptions
{
    public const string SectionName = "DemoSeed";

    public bool Enabled { get; set; }

    public string? AdminEmail { get; set; }

    public string? GuardianEmail { get; set; }

    /// <summary>Den fasta koden demokontona loggar in med. Sex siffror.</summary>
    public string Code { get; set; } = "424242";

    /// <summary>
    /// Sant om adressen är ett av de två demokontona och därför får logga in med den fasta
    /// koden i stället för en mejlad. Ett riktigt konto ger alltid <c>false</c>.
    /// </summary>
    public bool AllowsFixedCode(string normalizedEmail) =>
        Enabled
        && !string.IsNullOrWhiteSpace(Code)
        && (Matches(AdminEmail, normalizedEmail) || Matches(GuardianEmail, normalizedEmail));

    private static bool Matches(string? configured, string normalizedEmail) =>
        !string.IsNullOrWhiteSpace(configured)
        && string.Equals(configured.Trim(), normalizedEmail, StringComparison.OrdinalIgnoreCase);
}
