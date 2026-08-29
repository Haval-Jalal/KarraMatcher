namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Skickar en fråga till dess handler.
///
/// <para>
/// Controllers beror på det här interfacet och aldrig på en enskild handler. Det håller dem
/// tunna och gör att ett användningsfall kan bytas ut utan att API-lagret vet om det.
/// </para>
/// </summary>
public interface IQueryDispatcher
{
    public Task<TResult> SendAsync<TResult>(IQuery<TResult> query, CancellationToken cancellationToken);
}
