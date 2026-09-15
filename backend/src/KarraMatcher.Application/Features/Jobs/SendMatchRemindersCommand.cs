using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Jobs;

/// <summary>
/// Kör kvällspåminnelsen. Inga fält — jobbet vet själv vad "i morgon" betyder (`#64`).
/// </summary>
public sealed record SendMatchRemindersCommand : ICommand<int>;

internal sealed class SendMatchRemindersCommandHandler(MatchReminderService service)
    : ICommandHandler<SendMatchRemindersCommand, int>
{
    public Task<int> HandleAsync(
        SendMatchRemindersCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.SendDueRemindersAsync(cancellationToken);
    }
}
