namespace KarraMatcher.Application.Features.Administration;

/// <summary>Ett lag (kodnamn <c>Team</c>) så som superadmin-konsolen visar det (§KM.3, `#192`).</summary>
/// <param name="Id">Lagets id.</param>
/// <param name="TruppId">Truppen (AgeGroup) laget hör till.</param>
/// <param name="Name">Lagets namn, t.ex. "Gul".</param>
/// <param name="ColorHex">Lagfärgen som driver appens tema, t.ex. "#D9A21B".</param>
/// <param name="Slug">Stabil identifierare i URL:er — sätts vid skapande och ändras inte.</param>
public sealed record LagDto(Guid Id, Guid TruppId, string Name, string ColorHex, string Slug);
