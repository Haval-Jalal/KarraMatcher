namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Ett användningsfall som ändrar tillstånd (CQRS).
///
/// <para>
/// Skilt från <see cref="IQuery{TResult}"/> med flit, trots att mekaniken är densamma.
/// Skillnaden är inte teknisk utan avsedd att synas: en handler som tar ett
/// <c>ICommand</c> skriver, och den som tar en <c>IQuery</c> gör det aldrig. Vore de
/// samma interface hade den regeln bara varit en vana.
/// </para>
/// </summary>
/// <typeparam name="TResult">Vad kommandot svarar med när det lyckats.</typeparam>
#pragma warning disable CA1040 // Markörinterfacet bär resultattypen, precis som IQuery.
public interface ICommand<out TResult>;
#pragma warning restore CA1040
