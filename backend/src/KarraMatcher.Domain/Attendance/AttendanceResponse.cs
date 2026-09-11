namespace KarraMatcher.Domain.Attendance;

/// <summary>
/// En vuxens svar på en kallelse (`#57`, §KM.7).
///
/// <h3>Ett antal, aldrig ett barn</h3>
///
/// <para>
/// Den vuxna svarar för sin familj: Kommer / Kan inte / Kanske, och hur många som kommer.
/// Tränaren behöver veta om det blir elva på planen på lördag, och det svaret är ett antal
/// — inte en namnlista. Det finns med avsikt inget fält som pekar ut vilket barn som avses;
/// det vet bara familjens egen telefon (§KM.1, beslut 2026-09-10).
/// </para>
///
/// <h3>Ändringsbart till avspark</h3>
///
/// <para>
/// Planer ändras. Svaret går att ändra ända fram till avspark — samma post uppdateras, den
/// dubbleras aldrig. Efter avspark säger ett svar ingenting längre och tas inte emot.
/// </para>
/// </summary>
public sealed class AttendanceResponse
{
    public Guid Id { get; set; }

    /// <summary>Matchen svaret gäller.</summary>
    public Guid MatchId { get; set; }

    /// <summary>Kontot som svarade. Ett svar per konto och match.</summary>
    public Guid AccountId { get; set; }

    public AttendanceStatus Status { get; set; }

    /// <summary>
    /// Hur många ur familjen som kommer, 0–4.
    ///
    /// <para>
    /// Ett antal, inte en uppräkning. Fyra är taket därför att det är en rimlig familj till
    /// en match; gränsen prövas server-side, inte bara i formuläret.
    /// </para>
    /// </summary>
    public int Count { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime UpdatedUtc { get; set; }
}
