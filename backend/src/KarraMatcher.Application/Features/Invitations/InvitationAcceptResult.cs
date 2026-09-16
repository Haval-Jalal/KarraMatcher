namespace KarraMatcher.Application.Features.Invitations;

/// <summary>Utfallet av att acceptera en inbjudan (`#193`).</summary>
public enum InvitationAcceptOutcome
{
    /// <summary>Accepterad — förälderns medlemskap är skapat.</summary>
    Accepted,

    /// <summary>Token okänd — fel eller påhittad länk.</summary>
    NotFound,

    /// <summary>Länken har gått ut.</summary>
    Expired,

    /// <summary>Redan använd (eller återkallad) — kan inte accepteras igen.</summary>
    AlreadyUsed,

    /// <summary>Den inloggade är inte den adress inbjudan gäller.</summary>
    EmailMismatch,
}

/// <summary>Utfallet, och truppens namn när det gick vägen (för en vänlig kvittens).</summary>
public sealed record InvitationAcceptResult(InvitationAcceptOutcome Outcome, string? TruppName);
