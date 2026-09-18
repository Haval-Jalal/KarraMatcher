namespace KarraMatcher.Application.Features.Children;

/// <summary>Utfallet av att koppla en vårdnadshavare till ett barn (§KM.6, `#196`).</summary>
public enum LinkGuardianOutcome
{
    /// <summary>Kopplad.</summary>
    Linked,

    /// <summary>Barnet finns inte i truppen.</summary>
    ChildNotFound,

    /// <summary>Inget konto med den adressen.</summary>
    AccountNotFound,

    /// <summary>Kontot har inte gått med i truppen (ingen accepterad inbjudan/godkänd ansökan).</summary>
    NotTruppMember,

    /// <summary>Vårdnadshavaren har inte gett aktuellt samtycke (§KM.6).</summary>
    NoConsent,

    /// <summary>Kontot är redan vårdnadshavare för barnet.</summary>
    AlreadyLinked,
}
