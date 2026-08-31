using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.VerifyLoginCode;

internal sealed class VerifyLoginCodeCommandHandler(LoginCodeService codes)
    : ICommandHandler<VerifyLoginCodeCommand, SessionTokens?>
{
    public Task<SessionTokens?> HandleAsync(
        VerifyLoginCodeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return codes.VerifyAsync(command.Email, command.Code, cancellationToken);
    }
}
