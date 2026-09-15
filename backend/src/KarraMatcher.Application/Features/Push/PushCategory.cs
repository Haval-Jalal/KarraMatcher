namespace KarraMatcher.Application.Features.Push;

/// <summary>
/// Vilket slags notis det är (`#65`). Det en förälder kan välja bort per lag.
///
/// <para>
/// Tre grovkorniga typer, inte en per händelse: en förälder tänker "matchändringar",
/// "samåkning", "påminnelser" — inte "flyttad tid" mot "ny plats". Finkornigt hade blivit
/// en inställningssida ingen orkar med.
/// </para>
/// </summary>
public enum PushCategory
{
    /// <summary>Ny, flyttad, ändrad eller inställd match (`#62`).</summary>
    MatchChange = 0,

    /// <summary>Samåkning: erbjudande, förfrågan, svar, tillbakadraget (`#63`).</summary>
    Carpool = 1,

    /// <summary>Påminnelser: kvällen före match (`#64`) och kallelsen (`#58`).</summary>
    Reminder = 2,
}
