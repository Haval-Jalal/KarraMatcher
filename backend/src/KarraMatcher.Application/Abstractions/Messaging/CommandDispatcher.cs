using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Slår upp handlern för ett kommando och kör den genom registrerade behaviors.
///
/// <para>
/// Mekaniskt identisk med <see cref="QueryDispatcher"/>, och det är avsiktligt. Att slå
/// ihop dem till en gemensam "meddelandedispatcher" hade sparat ett femtiotal rader men
/// krävt att den fungerande frågevägen skrevs om — en refaktorering som hör hemma i en
/// egen ändring, inte i den som inför inloggningen. Skillnaden mellan att läsa och att
/// skriva är dessutom värd att synas i typerna (se <see cref="ICommand{TResult}"/>).
/// </para>
/// </summary>
internal sealed class CommandDispatcher(IServiceProvider services) : ICommandDispatcher
{
    private static readonly ConcurrentDictionary<(Type Command, Type Result), object> Wrappers = new();

    public Task<TResult> SendAsync<TResult>(
        ICommand<TResult> command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var wrapper = (Wrapper<TResult>)Wrappers.GetOrAdd(
            (command.GetType(), typeof(TResult)),
            static key => Activator.CreateInstance(
                typeof(Wrapper<,>).MakeGenericType(key.Command, key.Result))
                ?? throw new InvalidOperationException(
                    $"Kunde inte skapa omslag för kommandot {key.Command.Name}."));

        return wrapper.SendAsync(command, services, cancellationToken);
    }

    private abstract class Wrapper<TResult>
    {
        public abstract Task<TResult> SendAsync(
            ICommand<TResult> command,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    private sealed class Wrapper<TCommand, TResult> : Wrapper<TResult>
        where TCommand : ICommand<TResult>
    {
        public override Task<TResult> SendAsync(
            ICommand<TResult> command,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var typed = (TCommand)command;
            var handler = services.GetRequiredService<ICommandHandler<TCommand, TResult>>();
            var behaviors = services.GetServices<ICommandBehavior<TCommand, TResult>>().ToArray();

            Func<Task<TResult>> pipeline = () => handler.HandleAsync(typed, cancellationToken);

            // Bakifrån, så att den först registrerade behavioren hamnar ytterst.
            for (var i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var inner = pipeline;
                pipeline = () => behavior.HandleAsync(typed, inner, cancellationToken);
            }

            return pipeline();
        }
    }
}
