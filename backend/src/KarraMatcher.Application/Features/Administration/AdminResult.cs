namespace KarraMatcher.Application.Features.Administration;

/// <summary>
/// Utfallet av en superadmin-åtgärd, så controllern kan svara med rätt statuskod (`#192`).
///
/// <para>
/// Ett enkelt <c>null</c> räckte för matchhanteringen, där det enda felet var "finns inte".
/// Här kan en skapa- eller ändra-åtgärd falla på tre olika sätt — resursen finns inte, en
/// slug/ett namn är upptaget, eller en referens (klubb/sport) saknas — och de ska bli
/// <c>404</c>, <c>409</c> respektive <c>400</c>, inte alla samma.
/// </para>
/// </summary>
public enum AdminOutcome
{
    Success,
    NotFound,
    Conflict,
    ReferenceMissing,
}

/// <summary>Ett utfall med ett eventuellt värde. Se <see cref="AdminOutcome"/>.</summary>
public sealed record AdminResult<T>(T? Value, AdminOutcome Outcome)
    where T : class;

/// <summary>
/// Fabrik för <see cref="AdminResult{T}"/>. Egen, icke-generisk klass eftersom statiska
/// medlemmar inte hör hemma på en generisk typ (CA1000).
/// </summary>
public static class AdminResults
{
    public static AdminResult<T> Ok<T>(T value) where T : class => new(value, AdminOutcome.Success);

    public static AdminResult<T> NotFound<T>() where T : class => new(null, AdminOutcome.NotFound);

    public static AdminResult<T> Conflict<T>() where T : class => new(null, AdminOutcome.Conflict);

    public static AdminResult<T> ReferenceMissing<T>() where T : class =>
        new(null, AdminOutcome.ReferenceMissing);
}
