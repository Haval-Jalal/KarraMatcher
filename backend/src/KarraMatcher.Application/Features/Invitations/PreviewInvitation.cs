using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Invitations;

/// <summary>Förhandsvisar en inbjudan för landningssidan (`#193`). Null om token är okänd.</summary>
public sealed record PreviewInvitationQuery(string Token) : IQuery<InvitationPreviewDto?>;

internal sealed class PreviewInvitationQueryHandler(InvitationService service)
    : IQueryHandler<PreviewInvitationQuery, InvitationPreviewDto?>
{
    public Task<InvitationPreviewDto?> HandleAsync(
        PreviewInvitationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return service.PreviewAsync(query.Token, cancellationToken);
    }
}
