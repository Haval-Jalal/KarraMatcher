namespace KarraMatcher.Application.Tests;

/// <summary>
/// En klocka som testet flyttar fram själv.
///
/// <para>
/// Sessionernas livstider mäts i dagar — refresh-token lever i 60. Ett test som väntade in
/// en riktig utgång hade tagit två månader.
/// </para>
/// </summary>
internal sealed class FakeClock(DateTime startUtc) : TimeProvider
{
    private DateTime _now = startUtc;

    public override DateTimeOffset GetUtcNow() => new(_now, TimeSpan.Zero);

    public void Advance(TimeSpan amount) => _now = _now.Add(amount);
}
