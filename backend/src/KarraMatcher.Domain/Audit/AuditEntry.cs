namespace KarraMatcher.Domain.Audit;

/// <summary>
/// En känslig åtgärd, sparad för att gå att svara på frågan "vad hände".
///
/// <para>
/// §KM.10 kräver audit-logg för bland annat kontoradering. Posten är avsiktligt mager:
/// <b>vem uttryckt som id, vad, och när</b> — aldrig en adress, aldrig ett namn, aldrig
/// någon fritext från en användare. En audit-logg som samlar personuppgifter är själv ett
/// integritetsproblem, och den skulle dessutom överleva just den radering den beskriver.
/// </para>
///
/// <para>
/// <b>Ingen främmande nyckel till kontot.</b> Det är hela poängen: posten om en radering
/// måste finnas kvar efter att kontot är borta. En nyckel hade antingen raderat posten
/// med kontot eller hindrat raderingen.
/// </para>
///
/// <para>
/// Oföränderlig i praktiken: det finns bara ett sätt att skriva hit, och inget sätt att
/// ändra eller ta bort.
/// </para>
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; set; }

    /// <summary>Vad som hände, som en fast kod — inte en fritext som driver isär.</summary>
    public required string Action { get; set; }

    /// <summary>Vem, som id. Aldrig adressen.</summary>
    public Guid ActorAccountId { get; set; }

    /// <summary>Vad åtgärden gällde — matchens id, kontots id. Tomt när det inte finns.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>
    /// Före- och eftervärde, kort och maskinvänligt.
    ///
    /// <para>
    /// <b>Aldrig fritext från en användare.</b> Matchnotisen är tränarens egna ord och
    /// räknas som potentiell PII (§KM.1) — den får inte hamna här bara för att den ändrats.
    /// Fältet innehåller tid, motståndare, plats och status, alltså sådant som ändå står i
    /// den publika kalendern.
    /// </para>
    /// </summary>
    public string? Details { get; set; }

    public DateTime OccurredUtc { get; set; }
}

/// <summary>Åtgärderna som audit-loggas. Växer allteftersom §KM.10:s lista byggs.</summary>
public static class AuditActions
{
    public const string AccountDeleted = "konto.raderat";
    public const string AccountNameChanged = "konto.namn.andrat";

    public const string MatchCreated = "match.skapad";
    public const string MatchUpdated = "match.andrad";
    public const string MatchCancelled = "match.installd";
    public const string MatchDeleted = "match.raderad";

    // Superadmins plattformshantering (§KM.3, `#192`).
    public const string SportCreated = "sport.skapad";
    public const string SportUpdated = "sport.andrad";
    public const string ClubCreated = "klubb.skapad";
    public const string ClubUpdated = "klubb.andrad";
    public const string TruppCreated = "trupp.skapad";
    public const string TruppUpdated = "trupp.andrad";
    public const string LagCreated = "lag.skapat";
    public const string LagUpdated = "lag.andrat";
    public const string AdminGranted = "admin.tilldelad";
    public const string AdminRevoked = "admin.aterkallad";

    // Inbjudningar (§KM.3, `#193`).
    public const string InvitationCreated = "inbjudan.skapad";
    public const string InvitationAccepted = "inbjudan.accepterad";
    public const string InvitationRevoked = "inbjudan.aterkallad";

    // Ansökningar (§KM.3, `#194`).
    public const string ApplicationSubmitted = "ansokan.inskickad";
    public const string ApplicationApproved = "ansokan.godkand";
    public const string ApplicationDenied = "ansokan.nekad";

    public const string AttendanceEnabled = "narvaro.paslagen";
    public const string AttendanceDisabled = "narvaro.avslagen";
    public const string AttendanceCallOpened = "narvaro.kallelse.oppnad";

    public const string CarpoolOfferCreated = "samakning.erbjudande.skapat";
    public const string CarpoolOfferWithdrawn = "samakning.erbjudande.tillbakadraget";
    public const string CarpoolRequestCreated = "samakning.forfragan.skickad";
    public const string CarpoolRequestRetracted = "samakning.forfragan.atertagen";
    public const string CarpoolRequestAccepted = "samakning.forfragan.accepterad";
    public const string CarpoolRequestDenied = "samakning.forfragan.nekad";
}
