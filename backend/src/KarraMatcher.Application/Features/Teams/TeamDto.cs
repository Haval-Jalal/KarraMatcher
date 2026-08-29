namespace KarraMatcher.Application.Features.Teams;

/// <summary>
/// Ett lag så som appen visar det. Inga personuppgifter — endpointen är publik (§KM.3).
/// </summary>
/// <param name="Slug">Identifierar laget i URL:er.</param>
/// <param name="Name">Lagets namn, t.ex. "Gul".</param>
/// <param name="AgeGroup">Åldersgruppen, t.ex. "P2016".</param>
/// <param name="ColorHex">Lagfärgen som driver appens tema.</param>
public sealed record TeamDto(string Slug, string Name, string AgeGroup, string ColorHex);
