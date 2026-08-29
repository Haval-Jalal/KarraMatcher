using FluentValidation;

namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Kör frågans validatorer innan handlern får se den.
///
/// <para>
/// Att validera här och inte i varje handler betyder att en ny fråga är validerad så snart
/// någon skriver en validator för den — ingen kan glömma att anropa den. Saknas validator
/// passerar frågan, vilket är rätt för frågor utan parametrar.
/// </para>
///
/// <para>
/// Alla fel samlas ihop och kastas som en enda <see cref="ValidationException"/>, så att
/// användaren ser allt som är fel på en gång i stället för ett fel i taget.
/// </para>
/// </summary>
internal sealed class ValidationBehavior<TQuery, TResult>(
    IEnumerable<IValidator<TQuery>> validators) : IQueryBehavior<TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    public async Task<TResult> HandleAsync(
        TQuery query,
        Func<Task<TResult>> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var applicable = validators.ToArray();

        if (applicable.Length == 0)
        {
            return await continuation().ConfigureAwait(false);
        }

        var context = new ValidationContext<TQuery>(query);

        var failures = (await Task.WhenAll(
                applicable.Select(v => v.ValidateAsync(context, cancellationToken)))
            .ConfigureAwait(false))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            throw new ValidationException(failures);
        }

        return await continuation().ConfigureAwait(false);
    }
}
