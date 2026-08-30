namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Ett steg som läggs runt varje kommando — validering, och senare det som behövs.
/// </summary>
public interface ICommandBehavior<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(
        TCommand command,
        Func<Task<TResult>> continuation,
        CancellationToken cancellationToken);
}
