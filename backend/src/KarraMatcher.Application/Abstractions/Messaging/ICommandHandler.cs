namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>Hanteraren för exakt ett kommando. Ett användningsfall per handler.</summary>
public interface ICommandHandler<in TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
