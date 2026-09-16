namespace KarraMatcher.Application.Features.Administration;

/// <summary>En klubb så som superadmin-konsolen visar den (§KM.3, `#192`).</summary>
/// <param name="Id">Klubbens id.</param>
/// <param name="Name">Namnet, t.ex. "Kärra KIF".</param>
/// <param name="Slug">Stabil identifierare i URL:er — sätts vid skapande och ändras inte.</param>
public sealed record ClubDto(Guid Id, string Name, string Slug);
