namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Inställningen för edge-skyddet (§KM.11, checklista 4.6, <c>#584</c>).
///
/// <para>
/// <see cref="SharedSecret"/> är tom som förval och <b>måste</b> så förbli i repot — den sätts
/// som miljövariabel i Render (<c>EdgeAuth__SharedSecret</c>) och i Vercel (<c>KARRA_EDGE_AUTH</c>),
/// aldrig i en incheckad fil (§0 p.7). Är den tom är grinden inert, så koden kan rullas ut utan
/// att något går sönder innan hemligheten finns på båda sidor — se utrullningsordningen i
/// <c>EdgeAuthentication</c>.
/// </para>
/// </summary>
public sealed class EdgeAuthOptions
{
    /// <summary>
    /// Den delade hemligheten som Vercels edge skickar med i <see cref="EdgeAuthentication.HeaderName"/>
    /// på varje vidarebefordrad request. Tom = grinden avstängd.
    /// </summary>
    public string SharedSecret { get; set; } = string.Empty;
}
