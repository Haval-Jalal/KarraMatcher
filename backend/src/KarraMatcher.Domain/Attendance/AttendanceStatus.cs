namespace KarraMatcher.Domain.Attendance;

/// <summary>
/// Hur en vuxen svarar på en kallelse (§KM.7, `#57`).
///
/// <para>
/// Svaret gäller <b>familjen</b>, inte ett namngivet barn. Det finns med avsikt inget
/// värde som pekar ut vem som kommer — bara om, och hur många. Vilket barn som avses vet
/// bara familjens egen telefon (§KM.1, beslut 2026-09-10).
/// </para>
///
/// <para>
/// Text och inte siffra i databasen: en <c>2</c> i en logg säger ingenting den dag någon
/// felsöker, och en enum som får ett värde infogat i mitten ändrar då tyst betydelsen av
/// allt som redan sparats.
/// </para>
/// </summary>
public enum AttendanceStatus
{
    /// <summary>Kommer, med det antal som anges.</summary>
    Coming = 0,

    /// <summary>Kan inte komma. Antalet är då noll.</summary>
    CantCome = 1,

    /// <summary>Kanske — ännu inte bestämt. Antalet är det man tror i dag.</summary>
    Maybe = 2,
}
