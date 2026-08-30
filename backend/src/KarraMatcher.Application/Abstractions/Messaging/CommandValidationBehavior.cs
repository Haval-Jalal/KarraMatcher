using FluentValidation;

namespace KarraMatcher.Application.Abstractions.Messaging;

/// <summary>
/// Kör kommandots validatorer innan handlern får se det.
///
/// <para>
/// Samma resonemang som för frågornas <see cref="ValidationBehavior{TQuery, TResult}"/>:
/// ett nytt kommando är validerat så snart någon skriver en validator för det, och ingen
/// kan glömma att anropa den. För ett kommando väger det tyngre än för en fråga — här
/// skrivs det till databasen, och ovaliderad indata når annars ända fram.
/// </para>
/// </summary>
internal sealed class CommandValidationBehavior<TCommand, TResult>(
    IEnumerable<IValidator<TCommand>> validators) : ICommandBehavior<TCommand, TResult>
    where TCommand : ICommand<TResult>
{
    public async Task<TResult> HandleAsync(
        TCommand command,
        Func<Task<TResult>> continuation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var applicable = validators.ToArray();

        if (applicable.Length == 0)
        {
            return await continuation().ConfigureAwait(false);
        }

        var context = new ValidationContext<TCommand>(command);

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
