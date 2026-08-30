using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.SignOut;

internal sealed class SignOutCommandHandler(SessionIssuer sessions)
    : ICommandHandler<SignOutCommand, Unit>
{
    public async Task<Unit> HandleAsync(SignOutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await sessions.SignOutAsync(command.RefreshToken, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
