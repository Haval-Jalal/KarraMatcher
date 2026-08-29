namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>Hanteraren för exakt en fråga. Ett användningsfall per handler.</summary>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
