namespace KarraMatcher.Application.Features.Administration;

/// <summary>En sport så som superadmin-konsolen visar den (§KM.3, `#192`).</summary>
/// <param name="Id">Sportens id.</param>
/// <param name="Name">Namnet, t.ex. "Fotboll".</param>
/// <param name="Slug">Stabil identifierare i URL:er — sätts vid skapande och ändras inte.</param>
public sealed record SportDto(Guid Id, string Name, string Slug);
