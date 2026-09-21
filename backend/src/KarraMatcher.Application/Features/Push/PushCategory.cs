namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Vilket slags notis det är (`#65`, `#200`). Det en medlem kan välja bort per lag.
///
/// <para>
/// Grovkorniga typer, inte en per händelse: en förälder tänker "händelser", "kallelser",
/// "samåkning" — inte "flyttad tid" mot "ny plats". Finkornigt hade blivit en
/// inställningssida ingen orkar med. Värdena lagras aldrig (kön är i minnet, inställningen
/// är booleska kolumner), så ordningen kan ändras fritt.
/// </para>
/// </summary>
public enum PushCategory
{
    /// <summary>Händelse skapad, flyttad, ändrad eller inställd, samt kvällspåminnelsen (`#62`, `#64`).</summary>
    EventChange = 0,

    /// <summary>Kallelse: ny kallelse och påminnelse att svara (§KM.7, `#199`).</summary>
    Kallelse = 1,

    /// <summary>Samåkning: erbjudande, förfrågan, svar, tillbakadraget (`#63`).</summary>
    Carpool = 2,

    /// <summary>Chatt (byggs i `#201`/`#202`). Inställningen finns redan, utskicket kommer senare.</summary>
    Chat = 3,
}
