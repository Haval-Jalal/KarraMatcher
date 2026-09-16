using KarraMatcher.Domain.Invitations;

namespace KarraMatcher.Application.Features.Invitations;

/// <summary>Enda stället där en inbjudan blir en DTO (`#193`). Aldrig token-hashen.</summary>
internal static class InvitationMapping
{
    public static InvitationDto ToDto(this Invitation invitation)
    {
        ArgumentNullException.ThrowIfNull(invitation);

        return new InvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Status.ToString(),
            invitation.TeamId,
            invitation.Team?.Name,
            new DateTimeOffset(invitation.CreatedUtc, TimeSpan.Zero),
            new DateTimeOffset(invitation.ExpiresUtc, TimeSpan.Zero));
    }
}
