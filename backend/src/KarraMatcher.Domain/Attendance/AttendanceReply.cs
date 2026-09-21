namespace KarraMatcher.Domain.Attendance;

/// <summary>
/// En vårdnadshavares svar på en kallelse, per barn (§KM.7, `#199`).
///
/// <para>
/// Bara Ja eller Nej — inget "kanske". En kallelse ska ge tränaren ett tydligt underlag för
/// om laget blir fullt, och ett "kanske" är inget att planera efter. Ett obesvarat barn har
/// helt enkelt inget värde satt (raden bär ett nullbart svar), inte ett tredje alternativ.
/// </para>
///
/// <para>
/// Text och inte siffra i databasen: en <c>1</c> i en logg säger ingenting den dag någon
/// felsöker.
/// </para>
/// </summary>
public enum AttendanceReply
{
    /// <summary>Barnet kommer.</summary>
    Coming = 0,

    /// <summary>Barnet kommer inte.</summary>
    NotComing = 1,
}
