namespace KarraMatcher.Domain.Applications;

/// <summary>Var en ansökan är i sitt liv (`#194`).</summary>
public enum ApplicationStatus
{
    /// <summary>Inskickad, väntar på att en admin avgör den.</summary>
    Pending = 1,

    /// <summary>Godkänd — utgör förälderns medlemskap i truppen.</summary>
    Approved = 2,

    /// <summary>Nekad av en admin.</summary>
    Denied = 3,
}

/// <summary>
/// En förälders ansökan om att gå med i en trupp (`#194`, §KM.3).
///
/// <para>
/// Den andra vägen in vid sidan av inbjudan (`#193`): här tar föräldern initiativet och en
/// admin godkänner. <b>En godkänd ansökan är förälderns medlemskap</b> — samma modell som en
/// accepterad inbjudan, läst av <c>MembershipService</c>. Det finns ingen separat
/// medlemstabell.
/// </para>
///
/// <para>
/// <b>Inget om barn här.</b> Ansökan är enbart vuxen↔trupp. Barnets namn och samtycke
/// (§KM.1, §KM.6) hör till när barnet faktiskt kopplas (`#196`), inte hit.
/// </para>
/// </summary>
public sealed class MembershipApplication
{
    public Guid Id { get; set; }

    /// <summary>Truppen (kodnamn <c>AgeGroup</c>) ansökan gäller.</summary>
    public Guid AgeGroupId { get; set; }

    public Teams.AgeGroup? AgeGroup { get; set; }

    /// <summary>Kontot som ansökte. Raderas kontot försvinner ansökan (§KM.6).</summary>
    public Guid AccountId { get; set; }

    public Accounts.Account? Account { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;

    public DateTime CreatedUtc { get; set; }

    public DateTime? ResolvedUtc { get; set; }

    /// <summary>Adminen som avgjorde ansökan. Id, aldrig adress (§KM.10). Ingen FK.</summary>
    public Guid? ResolvedByAccountId { get; set; }
}
