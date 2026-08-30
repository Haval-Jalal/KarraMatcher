using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.RefreshSession;

internal sealed class RefreshSessionCommandHandler(SessionIssuer sessions)
    : ICommandHandler<RefreshSessionCommand, SessionTokens?>
{
    public Task<SessionTokens?> HandleAsync(
        RefreshSessionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return sessions.RefreshAsync(command.RefreshToken, cancellationToken);
    }
}
