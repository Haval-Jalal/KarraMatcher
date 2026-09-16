using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Administration;

namespace KarraMatcher.Application.Features.Consent;

/// <summary>En vårdnadshavare samtycker till en version av samtyckestexten (`#195`).</summary>
public sealed record GrantConsentCommand(Guid AccountId, string Version) : ICommand<AdminOutcome>;

internal sealed class GrantConsentCommandHandler(ConsentService service)
    : ICommandHandler<GrantConsentCommand, AdminOutcome>
{
    public Task<AdminOutcome> HandleAsync(
        GrantConsentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.GrantAsync(command.AccountId, command.Version, cancellationToken);
    }
}
