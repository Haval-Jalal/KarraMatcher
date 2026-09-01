namespace KarraMatcher.Application.Abstractions.Audit;

/// <summary>
/// Skriver en rad i audit-loggen.
///
/// <para>
/// Bara skrivning. Det finns med flit ingen metod för att läsa, ändra eller ta bort —
/// en audit-logg som går att redigera från appen är ingen audit-logg.
/// </para>
/// </summary>
public interface IAuditLog
{
    public Task RecordAsync(string action, Guid actorAccountId, CancellationToken cancellationToken);
}
