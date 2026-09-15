namespace KarraMatcher.Application.Features.Jobs;

/// <summary>
/// Hemligheten som skyddar de schemalagda jobbens endpoints (`#64`, §KM.11).
///
/// <h3>Varför en delad hemlighet och inte inloggning</h3>
///
/// <para>
/// Jobbet anropas av Vercels cron, inte av en människa — det finns ingen session att kräva.
/// I stället bär anropet en delad hemlighet i <c>Authorization</c>-headern, som jämförs mot
/// den här. Samma värde sätts i Vercel (<c>CRON_SECRET</c>) och i Render (<c>Jobs__Secret</c>);
/// rewriten skickar headern vidare, så backend ser den.
/// </para>
///
/// <h3>Saknas den är jobbet stängt, inte öppet</h3>
///
/// <para>
/// Är hemligheten inte satt avvisas <em>varje</em> anrop. Ett jobb som körde utan skydd
/// vore en väg för vem som helst att avfyra notiser till hundra föräldrar. Att den inte är
/// satt betyder att kvällspåminnelsen inte går ut — inte att den går ut till vem som helst.
/// </para>
/// </summary>
public sealed class JobOptions
{
    public const string SectionName = "Jobs";

    /// <summary>Delad hemlighet. Tom = jobben avvisar allt. Aldrig i kod eller incheckad config.</summary>
    public string Secret { get; set; } = string.Empty;
}
