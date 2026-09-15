using KarraMatcher.Application.Features.Matches;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser och märker matcher för kvällspåminnelsen (`#64`).
///
/// <para>
/// Egen abstraktion och inte en metod på matchhanteringen: det här är jobbets läsväg, och
/// den enda som bryr sig om vilka matcher som ännu inte påmints om. Att hålla den för sig
/// gör det synligt vem som märker en match som påmind — jobbet, och ingen annan.
/// </para>
/// </summary>
public interface IMatchReminderRepository
{
    /// <summary>
    /// Matcher med avspark i <c>[fromUtc, toUtc)</c> som ännu inte påmints om och inte är
    /// inställda. Inställda tas bort med flit: ingen ska påminnas om en match som inte spelas.
    /// </summary>
    public Task<IReadOnlyList<DueMatch>> ListDueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Märker matcherna som påminda. Det är det som gör jobbet idempotent — en andra körning
    /// hittar dem inte längre i <see cref="ListDueAsync"/>.
    /// </summary>
    public Task MarkRemindedAsync(
        IReadOnlyCollection<Guid> matchIds,
        DateTime sentUtc,
        CancellationToken cancellationToken);
}
