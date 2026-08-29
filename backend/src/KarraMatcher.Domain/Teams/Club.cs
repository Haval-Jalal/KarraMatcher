namespace KarraMatcher.Domain.Teams;

/// <summary>Föreningen. En enda i dag, men modellen är förberedd för fler.</summary>
public sealed class Club
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    /// <summary>Används i URL:er. Små bokstäver, inga svenska tecken.</summary>
    public required string Slug { get; set; }

    public ICollection<AgeGroup> AgeGroups { get; } = [];
}
