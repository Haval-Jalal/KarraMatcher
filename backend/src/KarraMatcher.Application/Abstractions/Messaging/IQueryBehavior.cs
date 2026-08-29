namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Ett steg i kedjan runt en handler — validering i dag, på sikt kanske loggning eller
/// cachning. Anropa <paramref name="continuation"/> för att gå vidare, eller låt bli för
/// att avbryta.
///
/// <para>
/// Parametern heter <c>continuation</c> och inte <c>next</c> eftersom det senare är ett
/// reserverat nyckelord i andra .NET-språk (CA1716).
/// </para>
/// </summary>
public interface IQueryBehavior<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public Task<TResult> HandleAsync(
        TQuery query,
        Func<Task<TResult>> continuation,
        CancellationToken cancellationToken);
}
