namespace KarraMatcher.Domain.Teams;

/// <summary>
/// En sport — fotboll, handboll, innebandy … (v2, `#190`).
///
/// <para>
/// Fristående från klubben: en klubb (Kärra KIF) driver flera sporter, och en trupp
/// (<see cref="AgeGroup"/>) länkar både klubb och sport. Så kan Kärra ha både fotboll P2016
/// och handboll P2016 utan att modellen antar något sportspecifikt.
/// </para>
/// </summary>
public sealed class Sport
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>URL-vänlig slug, gemener. T.ex. <c>fotboll</c>.</summary>
    public required string Slug { get; set; }

    public ICollection<AgeGroup> AgeGroups { get; } = [];
}
