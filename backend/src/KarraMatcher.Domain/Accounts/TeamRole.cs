namespace KarraMatcher.Domain.Accounts;

/// <summary>Vad ett konto får göra. Rollen bär ett scope beroende på sort (v2, `#190`).</summary>
public enum RoleKind
{
    /// <summary>Tränare för ett bestämt lag. Kräver <see cref="TeamRole.TeamId"/>.</summary>
    Coach = 1,

    /// <summary>
    /// Administratör för en <b>trupp</b> (v2). Kräver <see cref="TeamRole.AgeGroupId"/> —
    /// bjuder in föräldrar, sorterar barn i lag och tillsätter tränare i sin egen trupp.
    /// </summary>
    Admin = 2,

    /// <summary>
    /// Superadmin — global, äger plattformen (v2). Inget scope: varken lag eller trupp.
    /// Skapar sporter/klubbar/trupper/lag och tillsätter admins. Bara ägaren.
    /// </summary>
    SuperAdmin = 3,
}

/// <summary>
/// En roll ett konto har, med ett scope som beror på rollsorten (v2, `#190`).
///
/// <para>
/// <b>Scope per rollsort:</b> <see cref="RoleKind.Coach"/> är bunden till ett lag
/// (<see cref="TeamId"/>); <see cref="RoleKind.Admin"/> till en trupp
/// (<see cref="AgeGroupId"/>); <see cref="RoleKind.SuperAdmin"/> är global (båda tomma).
/// Kombinationen bevakas av ett villkor i databasen — en tränare utan lag, en admin utan
/// trupp, eller en superadmin med scope är inte giltiga tillstånd.
/// </para>
///
/// <para>
/// Vårdnadshavare är <em>inte</em> en roll här — det är relationen
/// <see cref="Children.Guardianship"/> mellan ett konto och ett barn.
/// </para>
/// </summary>
public sealed class TeamRole
{
    public Guid Id { get; set; }

    public Guid AccountId { get; set; }

    public Account? Account { get; set; }

    /// <summary>Laget rollen gäller. Satt endast för <see cref="RoleKind.Coach"/>.</summary>
    public Guid? TeamId { get; set; }

    public Teams.Team? Team { get; set; }

    /// <summary>Truppen rollen gäller. Satt endast för <see cref="RoleKind.Admin"/> (v2).</summary>
    public Guid? AgeGroupId { get; set; }

    public Teams.AgeGroup? AgeGroup { get; set; }

    public RoleKind Role { get; set; }

    public DateTime GrantedUtc { get; set; }
}
