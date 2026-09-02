namespace KarraMatcher.Application.Abstractions.Persistence;

/// <summary>
/// Det parsern behöver veta för att kunna lösa referenser.
///
/// <para>
/// Lagnamnen finns i flera former, eftersom ett inklistrat schema kan skriva laget som
/// "Kärra KIF P2016 Gul", "P2016 Gul" eller bara "Gul". Att kräva exakt en form vore att
/// be tränaren skriva om schemat, vilket är precis det massinlägget finns för att slippa.
/// </para>
/// </summary>
public sealed record ImportWorld(
    IReadOnlyDictionary<string, string> TeamsByName,
    IReadOnlyDictionary<string, Guid> VenuesByName,
    IReadOnlySet<string> ExistingMatchKeys);

public interface IScheduleImportRepository
{
    public Task<ImportWorld> LoadAsync(string teamSlug, CancellationToken cancellationToken);
}
