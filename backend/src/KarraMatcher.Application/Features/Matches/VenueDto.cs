namespace KarraMatcher.Application.Features.Matches;

/// <summary>
/// Spelplatsen. Koordinaterna driver väderprognosen och kartlänken — och de kommer alltid
/// från vår egen databas, aldrig från användarindata (SSRF-regeln i CLAUDE.md).
/// </summary>
public sealed record VenueDto(string Name, string Address, double Latitude, double Longitude);
