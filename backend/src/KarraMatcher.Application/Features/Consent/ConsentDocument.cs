namespace KarraMatcher.Application.Features.Consent;

/// <summary>
/// Samtyckestexten och dess versioner (§KM.6, `#195`).
///
/// <para>
/// Versionsregister i kod: varje version bor i källkoden, så att "visa vad man samtyckte
/// till" alltid kan slå upp exakt den text en vårdnadshavare godkände — även efter att texten
/// uppdaterats. Gamla versioner tas aldrig bort härifrån. Ändras texten: lägg till en ny
/// version och peka <see cref="CurrentVersion"/> på den.
/// </para>
/// </summary>
public static class ConsentDocument
{
    /// <summary>Den version en ny vårdnadshavare samtycker till i dag.</summary>
    public const string CurrentVersion = "1";

    private static readonly Dictionary<string, string> Texts =
        new(StringComparer.Ordinal)
        {
            ["1"] = """
                Samtycke till att spara uppgifter om ditt barn

                För att ditt barn ska kunna finnas i ett lag och få kallelser sparar Kärra
                Matcher en minimal uppgift om barnet på servern: barnets förnamn och första
                bokstaven i efternamnet (till exempel "Liam J"), samt vilken trupp och vilket
                lag barnet hör till. Inget mer.

                Vi sparar aldrig barnets hela efternamn, personnummer, födelsedatum, adress,
                telefonnummer, foto eller hälsouppgifter.

                Barnets resultat, mål och märken (spelarkortet) sparas bara i din egen telefon
                och lämnar aldrig servern.

                Den lagliga grunden är ditt samtycke. Du kan när som helst ta bort barnet, och
                då raderas uppgifterna direkt. Raderar du ditt konto tas allt du äger bort.

                Vi sparar vilken version av den här texten du godkände och när, så att det
                alltid går att se vad du samtyckte till.
                """,
        };

    /// <summary>Den aktuella texten.</summary>
    public static string Current => Texts[CurrentVersion];

    /// <summary>Texten för en viss version, eller null om versionen är okänd.</summary>
    public static string? TextFor(string version)
    {
        ArgumentNullException.ThrowIfNull(version);

        return Texts.TryGetValue(version, out var text) ? text : null;
    }
}
