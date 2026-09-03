using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Det föraren fyller i. Exakt de fält §KM.12 räknar upp, varken fler eller färre.
/// </summary>
public sealed record CarpoolOfferDraft(
    CarpoolDirection Direction,
    string DeparturePlace,
    DateTime DepartureUtc,
    int Seats,
    string? Note);
