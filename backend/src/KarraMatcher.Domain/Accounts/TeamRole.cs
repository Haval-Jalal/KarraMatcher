namespace KarraMatcher.Domain.Accounts;

/// <summary>Vad ett konto får göra.</summary>
public enum RoleKind
{
    /// <summary>Tränare för ett bestämt lag. Kräver <see cref="TeamRole.TeamId"/>.</summary>
    Coach = 1,

    /// <summary>Administratör för alla lag. Har inget lag knutet till sig.</summary>
    Admin = 2,
}

/// <summary>
/// En roll ett konto har, eventuellt bunden till ett lag.
///
/// <para>
/// <b>Tränarrollen är alltid knuten till ett lag.</b> En tränare för Gul ska inte kunna
/// röra Blås matcher, och det är enklare att bygga in från början än att laga i efterhand
/// — därför finns ingen rollnivå som betyder "tränare i allmänhet".
/// </para>
///
/// <para>
/// Admin har <see cref="TeamId"/> tom, eftersom rollen inte gäller ett lag utan alla.
/// Kombinationen bevakas av ett villkor i databasen: en tränare utan lag, eller en admin
/// med ett, är inte ett giltigt tillstånd.
/// </para>
/// </summary>
public sealed class TeamRole
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    /// <summary>Laget rollen gäller. Tom för <see cref="RoleKind.Admin"/>.</summary>
    public Guid? TeamId { get; set; }

    public Teams.Team? Team { get; set; }

    public RoleKind Role { get; set; }

    public DateTime GrantedUtc { get; set; }
}
