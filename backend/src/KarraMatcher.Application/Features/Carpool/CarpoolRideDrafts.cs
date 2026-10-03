using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Features.Carpool;

/// <summary>
/// Det en förälder fyller i för att be om skjuts: riktning, antal platser som behövs och en valfri
/// rad till laget. Spegelbilden av erbjudandets utkast, minus avgångsplats/tid — den som behöver
/// skjuts bestämmer inte varifrån eller när (det gör föraren som erbjuder).
/// </summary>
public sealed record CarpoolRideRequestDraft(CarpoolDirection Direction, int Seats, string? Note);

/// <summary>Det en förare fyller i för att erbjuda plats på en skjutsförfrågan: antal platser och en valfri hälsning.</summary>
public sealed record CarpoolRideOfferDraft(int Seats, string? Message);
