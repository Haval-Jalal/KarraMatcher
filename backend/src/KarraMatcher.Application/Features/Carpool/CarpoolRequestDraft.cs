namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Det den som frågar fyller i. Exakt vad §KM.12 räknar upp: antal platser och en valfri
/// hälsning.
/// </summary>
public sealed record CarpoolRequestDraft(int Seats, string? Message);
