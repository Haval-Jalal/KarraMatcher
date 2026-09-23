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

    /// <summary>
    /// Requestens correlation-id (§KM.10) — samma id som loggraderna bär och som klienten
    /// får i <c>X-Correlation-Id</c>.
    ///
    /// <para>
    /// Det är det som gör posten spårbar: ringer en förälder om att något gick fel går
    /// audit-raden att koppla till just den requestens loggar, och tvärtom. Det är ett
    /// request-id, inte en personuppgift — aldrig ett namn, en adress eller fritext.
    /// Tomt när åtgärden inte skedde i en request (i dag skriver bara request-vägar hit).
    /// </para>
    /// </summary>
    public string? CorrelationId { get; set; }

    public DateTime OccurredUtc { get; set; }
}

/// <summary>Åtgärderna som audit-loggas. Växer allteftersom §KM.10:s lista byggs.</summary>
public static class AuditActions
{
    public const string AccountDeleted = "konto.raderat";
    public const string AccountNameChanged = "konto.namn.andrat";

    // Händelser (match/träning/övrigt) (§KM.10, `#198`).
    public const string EventCreated = "handelse.skapad";
    public const string EventUpdated = "handelse.andrad";
    public const string EventCancelled = "handelse.installd";
    public const string EventDeleted = "handelse.raderad";

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

    // Tränartillsättning per lag (§KM.3, `#197`).
    public const string CoachGranted = "tranare.tillsatt";
    public const string CoachRevoked = "tranare.avsatt";

    // Inbjudningar (§KM.3, `#193`).
    public const string InvitationCreated = "inbjudan.skapad";
    public const string InvitationAccepted = "inbjudan.accepterad";
    public const string InvitationRevoked = "inbjudan.aterkallad";

    // Ansökningar (§KM.3, `#194`).
    public const string ApplicationSubmitted = "ansokan.inskickad";
    public const string ApplicationApproved = "ansokan.godkand";
    public const string ApplicationDenied = "ansokan.nekad";

    // Vårdnadshavarsamtycke (§KM.6, `#195`).
    public const string ConsentGranted = "samtycke.givet";

    // Barnhantering (§KM.1, `#196`).
    public const string ChildCreated = "barn.skapat";
    public const string ChildUpdated = "barn.andrat";
    public const string ChildDeleted = "barn.raderat";
    public const string GuardianLinked = "vardnadshavare.kopplad";
    public const string GuardianUnlinked = "vardnadshavare.bortkopplad";

    public const string AttendanceEnabled = "narvaro.paslagen";
    public const string AttendanceDisabled = "narvaro.avslagen";
    public const string AttendanceCallOpened = "narvaro.kallelse.oppnad";

    // Chatt (§KM.1/§KM.10, `#201`) — aldrig meddelandetexten, bara id och åtgärd.
    public const string ChatMessageDeleted = "chatt.meddelande.raderat";
    public const string ChatMessageReported = "chatt.meddelande.anmalt";

    public const string CarpoolOfferCreated = "samakning.erbjudande.skapat";
    public const string CarpoolOfferWithdrawn = "samakning.erbjudande.tillbakadraget";
    public const string CarpoolRequestCreated = "samakning.forfragan.skickad";
    public const string CarpoolRequestRetracted = "samakning.forfragan.atertagen";
    public const string CarpoolRequestAccepted = "samakning.forfragan.accepterad";
    public const string CarpoolRequestDenied = "samakning.forfragan.nekad";
}
