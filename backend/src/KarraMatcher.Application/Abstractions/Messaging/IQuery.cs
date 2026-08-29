namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// En fråga som läser tillstånd utan att ändra det (CQRS).
///
/// <para>
/// Markörinterfacet bär resultattypen, så att <see cref="IQueryDispatcher"/> kan slå upp
/// rätt handler utan att anroparen behöver namnge den.
/// </para>
/// </summary>
/// <typeparam name="TResult">Vad frågan svarar med.</typeparam>
#pragma warning disable CA1040 // Markörinterfacet är hela poängen: det binder frågan till sin resultattyp.
public interface IQuery<out TResult>;
#pragma warning restore CA1040
