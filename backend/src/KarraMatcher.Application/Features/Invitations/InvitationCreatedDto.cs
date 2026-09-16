namespace KarraMatcher.Application.Features.Invitations;

/// <summary>
/// Svaret när en admin skapat en inbjudan (`#193`).
///
/// <para>
/// <b>Accept-länken returneras en gång.</b> Den mejlas till föräldern, men admin får den
/// också här så att den går att kopiera och skicka på annat sätt om mejlet inte kommer fram.
/// Token finns aldrig i någon lista efteråt — bara dess hash lagras.
/// </para>
/// </summary>
public sealed record InvitationCreatedDto(InvitationDto Invitation, string AcceptUrl);
