namespace KarraMatcher.Application.Features.Invitations;

/// <summary>En inbjudan som admin-vyn visar den (`#193`).</summary>
/// <param name="Id">Inbjudans id.</param>
/// <param name="Email">Adressen inbjudan skickades till.</param>
/// <param name="Status">Väntar / Accepterad / Återkallad.</param>
/// <param name="TeamId">Valfritt lag-förslag inom truppen.</param>
/// <param name="TeamName">Lagets namn, för visning.</param>
/// <param name="CreatedUtc">När den skapades.</param>
/// <param name="ExpiresUtc">När länken slutar gälla.</param>
public sealed record InvitationDto(
    Guid Id,
    string Email,
    string Status,
    Guid? TeamId,
    string? TeamName,
    DateTimeOffset CreatedUtc,
    DateTimeOffset ExpiresUtc);
