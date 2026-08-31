namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Skickar ett kommando till dess handler.
///
/// <para>
/// Controllers beror på det här interfacet och aldrig på en enskild handler — samma
/// resonemang som för <see cref="IQueryDispatcher"/>.
/// </para>
/// </summary>
public interface ICommandDispatcher
{
    public Task<TResult> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken);
}
