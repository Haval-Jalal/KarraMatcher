namespace KarraMatcher.Domain.Teams;

/// <summary>
/// En trupp — en åldersgrupp inom en klubb och en sport, t.ex. Kärra fotboll P2016 säsongen 2026.
///
/// <para>
/// "Trupp" i v2-termer (`#190`). En trupp länkar både klubb (<see cref="Club"/>) och sport
/// (<see cref="Sport"/>), och delas i sin tur i lag (<see cref="Team"/>, färgerna).
/// </para>
/// </summary>
public sealed class AgeGroup
{
    public Guid Id { get; set; }

    public Guid ClubId { get; set; }

    public Club? Club { get; set; }

    /// <summary>Sporten truppen tillhör (v2). En klubb kan ha trupper i flera sporter.</summary>
    public Guid SportId { get; set; }

    public Sport? Sport { get; set; }

    public required string Name { get; set; }

    public required string Season { get; set; }

    public ICollection<Team> Teams { get; } = [];
}
