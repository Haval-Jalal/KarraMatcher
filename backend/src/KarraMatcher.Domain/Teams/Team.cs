namespace KarraMatcher.Domain.Teams;

/// <summary>Ett lag inom en åldersgrupp — Gul, Blå, Vit eller Svart.</summary>
public sealed class Team
{
    public Guid Id { get; set; }

    public Guid AgeGroupId { get; set; }

    public AgeGroup? AgeGroup { get; set; }

    public required string Name { get; set; }

    /// <summary>Lagfärgen som hex, driver appens tema. T.ex. <c>#D9A21B</c>.</summary>
    public required string ColorHex { get; set; }

    public required string Slug { get; set; }

    public ICollection<Events.Event> Events { get; } = [];
}
