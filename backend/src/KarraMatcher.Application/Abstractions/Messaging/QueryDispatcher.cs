using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Slår upp handlern för en fråga och kör den genom registrerade behaviors.
///
/// <para>
/// Anroparen känner bara till <see cref="IQuery{TResult}"/>, medan handlern är registrerad
/// på sin konkreta frågetyp. Bryggan däremellan är den generiska omslagsklassen längst ned:
/// den skapas en gång per frågetyp med reflektion och sparas, så att själva anropet sedan
/// sker genom ett vanligt virtuellt anrop utan reflektion.
/// </para>
///
/// <para>
/// Varför inte MediatR: paketet ligger sedan version 13 under RPL-1.5, som kräver att vår
/// egen källkod publiceras vid driftsättning. Beslutet är infört i
/// <c>docs/PROJEKT-HANDOFF.md</c> under <em>Viktiga beslut</em>.
/// </para>
/// </summary>
internal sealed class QueryDispatcher(IServiceProvider services) : IQueryDispatcher
{
    /// <summary>
    /// Nyckeln är frågetypen <em>och</em> resultattypen. En typ kan i teorin implementera
    /// <see cref="IQuery{TResult}"/> mer än en gång, och då är frågetypen ensam inte unik.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type Query, Type Result), object> Wrappers = new();

    public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var wrapper = (Wrapper<TResult>)Wrappers.GetOrAdd(
            (query.GetType(), typeof(TResult)),
            static key => Activator.CreateInstance(
                typeof(Wrapper<,>).MakeGenericType(key.Query, key.Result))
                ?? throw new InvalidOperationException(
                    $"Kunde inte skapa omslag för frågan {key.Query.Name}."));

        return wrapper.SendAsync(query, services, cancellationToken);
    }

    private abstract class Wrapper<TResult>
    {
        public abstract Task<TResult> SendAsync(
            IQuery<TResult> query,
            IServiceProvider services,
            CancellationToken cancellationToken);
    }

    private sealed class Wrapper<TQuery, TResult> : Wrapper<TResult>
        where TQuery : IQuery<TResult>
    {
        public override Task<TResult> SendAsync(
            IQuery<TResult> query,
            IServiceProvider services,
            CancellationToken cancellationToken)
        {
            var typed = (TQuery)query;
            var handler = services.GetRequiredService<IQueryHandler<TQuery, TResult>>();
            var behaviors = services.GetServices<IQueryBehavior<TQuery, TResult>>().ToArray();

            Func<Task<TResult>> pipeline = () => handler.HandleAsync(typed, cancellationToken);

            // Bakifrån, så att den först registrerade behavioren hamnar ytterst och alltså
            // körs först. Valideringen ska hinna avbryta innan något annat händer.
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
