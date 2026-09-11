namespace KarraMatcher.Domain.Attendance;

/// <summary>
/// Att tränaren öppnat kallelsen för en match (`#57`, §KM.7).
///
/// <h3>Existens är hela innebörden</h3>
///
/// <para>
/// Finns posten är matchen kallad, och då kan föräldrar svara. Finns den inte har tränaren
/// inte kallat än, och ett svar hör ingenstans. Det är därför en egen post och inte en
/// flagga på matchen: en match kan bytas, ställas in och läggas upp av vem som helst med
/// tränarbehörighet — kallelsen är en särskild handling, med en tidpunkt.
/// </para>
///
/// <h3>Inget barn nämns</h3>
///
/// <para>
/// Kallelsen bär matchens id och när den öppnades. Den räknar svar från vuxna konton och
/// namnger aldrig ett barn (§KM.1, beslut 2026-09-10).
/// </para>
/// </summary>
public sealed class AttendanceCall
{
    public Guid Id { get; set; }

    /// <summary>Matchen kallelsen gäller. Unik — en match kallas en gång.</summary>
    public Guid MatchId { get; set; }

    /// <summary>
    /// Kontot som öppnade kallelsen. En vuxen tränare, aldrig ett barn.
    ///
    /// <para>
    /// Ingen främmande nyckel — som audit-raden överlever posten att tränarens konto
    /// raderas (§KM.6). Vem som kallade är en anteckning, inte en ägare: kallelsen tillhör
    /// matchen, inte den som råkade trycka på knappen.
    /// </para>
    /// </summary>
    public Guid OpenedByAccountId { get; set; }

    public DateTime OpenedUtc { get; set; }
}
