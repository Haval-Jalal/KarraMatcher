namespace KarraMatcher.Application.Features.Invitations;

/// <summary>
/// Det landningssidan visar innan man accepterar (`#193`).
///
/// <para>
/// Anonymt läsbar med token i handen — den som har länken får se vart den leder. Innehåller
/// bara truppens namn och adressen inbjudan gäller, så föräldern ser att det stämmer och kan
/// logga in som rätt adress. Inget om andra medlemmar eller barn.
/// </para>
/// </summary>
/// <param name="Valid">Sant om inbjudan går att acceptera (väntande och inte utgången).</param>
/// <param name="TruppName">Truppens namn.</param>
/// <param name="LagName">Lag-förslaget, om något.</param>
/// <param name="Email">Adressen inbjudan gäller — så föräldern loggar in som rätt person.</param>
public sealed record InvitationPreviewDto(
    bool Valid,
    string TruppName,
    string? LagName,
    string Email);
