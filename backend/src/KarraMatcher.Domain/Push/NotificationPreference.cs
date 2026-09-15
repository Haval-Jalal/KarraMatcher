namespace KarraMatcher.Domain.Push;

/// <summary>
/// En förälders val av vilka notiser hen vill ha för ett lag (`#65`).
///
/// <h3>Frånvaro betyder allt på</h3>
///
/// <para>
/// Finns ingen rad för ett konto och ett lag får kontot alla sorters notiser — det är det
/// rimliga förvalet, och det som gäller för den som aldrig varit inne på inställningarna.
/// En rad skapas först när någon <em>ändrar</em> något, och beskriver då vad som är kvar på.
/// </para>
///
/// <h3>Per lag, inte globalt</h3>
///
/// <para>
/// En förälder med barn i två lag ska kunna ha olika inställningar för dem — matchändringar
/// för det ena laget, allt för det andra. Därför är nyckeln kontot <em>och</em> laget.
/// </para>
/// </summary>
public sealed class NotificationPreference
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Guid TeamId { get; set; }

    /// <summary>Ny, flyttad, ändrad eller inställd match (`#62`).</summary>
    public bool MatchChanges { get; set; } = true;

    /// <summary>Samåkning: erbjudande, förfrågan, svar (`#63`).</summary>
    public bool Carpool { get; set; } = true;

    /// <summary>Påminnelser: kvällen före match (`#64`) och kallelsen (`#58`).</summary>
    public bool Reminders { get; set; } = true;

    public DateTime UpdatedUtc { get; set; }
}
