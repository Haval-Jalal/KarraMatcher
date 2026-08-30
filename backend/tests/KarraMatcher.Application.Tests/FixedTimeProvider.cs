namespace KarraMatcher.Application.Tests;

/// <summary>
/// En klocka som står still, så att kalenderfeedens <c>DTSTAMP</c> går att jämföra.
///
/// Tio rader egen kod i stället för <c>Microsoft.Extensions.TimeProvider.Testing</c>:
/// paketet kan mycket mer än vi behöver, och ett beroende som bara två tester använder är
/// ett beroende för mycket.
/// </summary>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
