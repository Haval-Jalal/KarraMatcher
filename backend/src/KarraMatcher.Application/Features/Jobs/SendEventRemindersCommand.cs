using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Jobs;

/// <summary>
/// Kör kvällspåminnelsen. Inga fält — jobbet vet själv vad "i morgon" betyder (`#64`).
/// </summary>
public sealed record SendEventRemindersCommand : ICommand<int>;

internal sealed class SendEventRemindersCommandHandler(EventReminderService service)
    : ICommandHandler<SendEventRemindersCommand, int>
{
    public Task<int> HandleAsync(
        SendEventRemindersCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return service.SendDueRemindersAsync(cancellationToken);
    }
}
