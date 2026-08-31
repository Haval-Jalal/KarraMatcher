using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.RequestLoginCode;

internal sealed class RequestLoginCodeCommandHandler(LoginCodeService codes)
    : ICommandHandler<RequestLoginCodeCommand, Unit>
{
    public async Task<Unit> HandleAsync(
        RequestLoginCodeCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await codes.RequestAsync(command.Email, cancellationToken).ConfigureAwait(false);

        return Unit.Value;
    }
}
