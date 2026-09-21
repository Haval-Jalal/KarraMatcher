namespace KarraMatcher.Domain.Attendance;

/// <summary>
/// Ett barn som är kallat till en händelse, med vårdnadshavarens svar (§KM.7, `#199`).
///
/// <h3>Raden är både kallelsen och svaret</h3>
///
/// <para>
/// Att raden finns betyder att barnet är kallat. <see cref="Reply"/> är null tills en
/// vårdnadshavare svarat — då bär den Ja eller Nej. Den här mängden rader <em>är</em>
/// kallelsens målgrupp: vilka barn som ombetts komma, tvärs över lagen i truppen.
/// </para>
///
/// <h3>Barnen väljs ur hela truppen</h3>
///
/// <para>
/// En match spelas av ett färg-lag, men om några inte kan fylls laget på med barn ur de
/// andra lagen. Därför pekar raden på ett <see cref="ChildId"/> och inte på ett lag — vilka
/// barn som kallas avgörs när kallelsen skickas, inte av vilket lag händelsen råkar tillhöra.
/// </para>
/// </summary>
public sealed class AttendanceInvitation
{
    public Guid Id { get; set; }

    /// <summary>Kallelsen barnet hör till (<see cref="AttendanceCall"/>).</summary>
    public Guid CallId { get; set; }

    /// <summary>Det kallade barnet. Ett barn kallas en gång per kallelse.</summary>
    public Guid ChildId { get; set; }

    /// <summary>Vårdnadshavarens svar, eller null tills någon svarat.</summary>
    public AttendanceReply? Reply { get; set; }

    /// <summary>
    /// Kontot som svarade, eller null. Ingen främmande nyckel — som audit-raden överlever
    /// svaret att kontot raderas; vem som svarade är en anteckning, inte en ägare.
    /// </summary>
    public Guid? RespondedByAccountId { get; set; }

    public DateTime? RespondedUtc { get; set; }
}
