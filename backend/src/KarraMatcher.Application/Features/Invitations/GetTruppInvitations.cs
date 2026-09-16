using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Features.Invitations;

/// <summary>Väntande inbjudningar för en trupp, för admin-vyn (`#193`).</summary>
public sealed record GetTruppInvitationsQuery(Guid AgeGroupId) : IQuery<IReadOnlyList<InvitationDto>>;

internal sealed class GetTruppInvitationsQueryHandler(IInvitationRepository invitations)
    : IQueryHandler<GetTruppInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    public async Task<IReadOnlyList<InvitationDto>> HandleAsync(
        GetTruppInvitationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pending = await invitations
            .GetPendingForTruppAsync(query.AgeGroupId, cancellationToken)
            .ConfigureAwait(false);

        return [.. pending.Select(invitation => invitation.ToDto())];
    }
}
