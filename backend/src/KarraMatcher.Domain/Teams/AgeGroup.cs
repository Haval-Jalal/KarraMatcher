namespace KarraMatcher.Domain.Teams;

/// <summary>En åldersgrupp inom föreningen, t.ex. P2016 säsongen 2026.</summary>
public sealed class AgeGroup
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    public Club? Club { get; set; }

    public required string Name { get; set; }

    public required string Season { get; set; }

    public ICollection<Team> Teams { get; } = [];
}
