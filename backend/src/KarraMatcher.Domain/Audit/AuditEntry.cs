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

    public DateTime OccurredUtc { get; set; }
}

/// <summary>Åtgärderna som audit-loggas. Växer allteftersom §KM.10:s lista byggs.</summary>
public static class AuditActions
{
    public const string AccountDeleted = "konto.raderat";
}
