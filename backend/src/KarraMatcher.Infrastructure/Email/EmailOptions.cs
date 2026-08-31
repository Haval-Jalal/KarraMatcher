namespace KarraMatcher.Infrastructure.Email;

/// <summary>Inställningar för utgående mejl.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>
    /// API-nyckeln hos Resend. Saknas den finns ingen leverantör att skicka med.
    ///
    /// <para>
    /// Kommer ur user-secrets lokalt och ur en miljövariabel i Render. Aldrig i kod och
    /// aldrig i incheckad appsettings.
    /// </para>
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Avsändaradressen.
    ///
    /// <para>
    /// Utan verifierad domän godtar Resend bara sin egen <c>onboarding@resend.dev</c>,
    /// och den levererar <b>enbart till kontots egen adress</b> — inte till föräldrarna.
    /// Det räcker för att bygga och pröva flödet, men inte för att lansera. Se öppen
    /// fråga 5 i handoff-filen.
    /// </para>
    /// </summary>
    public string From { get; set; } = "Karra Matcher <onboarding@resend.dev>";
}
