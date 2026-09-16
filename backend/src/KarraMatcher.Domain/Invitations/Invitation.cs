namespace KarraMatcher.Domain.Invitations;

/// <summary>Var en inbjudan är i sitt liv (`#193`).</summary>
public enum InvitationStatus
{
    /// <summary>Skickad, väntar på att accepteras.</summary>
    Pending = 1,

    /// <summary>Accepterad — utgör förälderns medlemskap i truppen.</summary>
    Accepted = 2,

    /// <summary>Återkallad av en admin innan den accepterades.</summary>
    Revoked = 3,
}

/// <summary>
/// En inbjudan för en vårdnadshavare att gå med i en trupp (`#193`, §KM.3).
///
/// <para>
/// <b>Inbjudan är också medlemskaps-liggaren.</b> En accepterad inbjudan är förälderns
/// medlemskap i truppen — det finns ingen separat "medlem"-tabell. Barnkopplingen
/// (<c>Guardianship</c>) läggs på senare (`#196`); här räcker att en vuxen bjudits in och
/// tackat ja.
/// </para>
///
/// <para>
/// <b>Bunden till en adress.</b> Inbjudan skickas till en bestämd e-postadress, och bara den
/// som loggar in som just den adressen kan acceptera — en vidarebefordrad länk hjälper inte
/// fel person in. Själva länk-token lagras aldrig i klartext, bara som hash (§KM.10), precis
/// som en inloggningskod.
/// </para>
/// </summary>
public sealed class Invitation
{
    public Guid Id { get; set; }

    /// <summary>Truppen (kodnamn <c>AgeGroup</c>) inbjudan gäller.</summary>
    public Guid AgeGroupId { get; set; }

    public Teams.AgeGroup? AgeGroup { get; set; }

    /// <summary>Valfritt lag inom truppen, som förslag inför sorteringen (`#196`).</summary>
    public Guid? TeamId { get; set; }

    public Teams.Team? Team { get; set; }

    /// <summary>Adressen inbjudan skickades till, normaliserad till gemener.</summary>
    public required string Email { get; set; }

    /// <summary>SHA-256 av länk-token. Klartexten lämnar servern bara i mejlet.</summary>
    public required string TokenHash { get; set; }

    /// <summary>Adminen som skapade inbjudan. Id, aldrig adress (§KM.10). Ingen FK.</summary>
    public Guid CreatedByAccountId { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime ExpiresUtc { get; set; }

    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;

    /// <summary>Kontot som accepterade — förälderns medlemskap. FK, så radering tar med det.</summary>
    public Guid? AcceptedByAccountId { get; set; }

    public Accounts.Account? AcceptedByAccount { get; set; }

    public DateTime? AcceptedUtc { get; set; }

    /// <summary>Går den att acceptera nu? Väntande och inte utgången.</summary>
    public bool IsPending(DateTime nowUtc) =>
        Status == InvitationStatus.Pending && ExpiresUtc > nowUtc;
}
