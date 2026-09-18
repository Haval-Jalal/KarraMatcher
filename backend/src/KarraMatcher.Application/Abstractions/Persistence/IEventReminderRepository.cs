using KarraMatcher.Application.Features.Events;

namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Läser och märker händelser för kvällspåminnelsen (`#64`, `#198`).
///
/// <para>
/// Egen abstraktion och inte en metod på händelsehanteringen: det här är jobbets läsväg, och
/// den enda som bryr sig om vilka händelser som ännu inte påmints om.
/// </para>
/// </summary>
public interface IEventReminderRepository
{
    /// <summary>
    /// Händelser med start i <c>[fromUtc, toUtc)</c> som ännu inte påmints om och inte är
    /// inställda. Inställda tas bort med flit: ingen ska påminnas om något som inte äger rum.
    /// </summary>
    public Task<IReadOnlyList<DueEvent>> ListDueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken);

    /// <summary>
    /// Märker händelserna som påminda. Det är det som gör jobbet idempotent — en andra
    /// körning hittar dem inte längre i <see cref="ListDueAsync"/>.
    /// </summary>
    public Task MarkRemindedAsync(
        IReadOnlyCollection<Guid> eventIds,
        DateTime sentUtc,
        CancellationToken cancellationToken);
}
