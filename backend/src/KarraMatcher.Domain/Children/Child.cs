namespace KarraMatcher.Domain.Children;

/// <summary>
/// Ett barn i en trupp (v2, `#190`) — en <b>minimal</b> profil enligt §KM.1.
///
/// <h3>Bara det som en funktion kräver</h3>
///
/// <para>
/// Lagras: förnamn, efternamnets <em>initial</em> och tillhörighet till trupp och lag. Visas
/// som t.ex. <c>Liam J</c> — <b>aldrig hela efternamnet</b>. Förbjudet här: personnummer,
/// födelsedatum, adress, foto, hälsa, position. Nya fält om ett barn kräver ett skrivet
/// beslut i handoff (§KM.1).
/// </para>
///
/// <h3>Detta är inte spelarkortet</h3>
///
/// <para>
/// Barnets statistik (resultat, mål, märken) bor kvar enbart på enheten (§KM.2) och har
/// ingen koppling hit. Den här posten säger bara <em>vem som hör till vilket lag</em>.
/// </para>
/// </summary>
public sealed class Child
{
    public Guid Id { get; set; }

    public required string FirstName { get; set; }

    /// <summary>Efternamnets initial, t.ex. <c>J</c>. Aldrig hela efternamnet (§KM.1).</summary>
    public required string LastInitial { get; set; }

    /// <summary>Truppen barnet hör till.</summary>
    public Guid AgeGroupId { get; set; }

    public Teams.AgeGroup? AgeGroup { get; set; }

    /// <summary>Laget (färgen) inom truppen, eller tomt tills en admin sorterat barnet.</summary>
    public Guid? TeamId { get; set; }

    public Teams.Team? Team { get; set; }

    public DateTime CreatedUtc { get; set; }
}
