namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Gör säkerhetsrelevanta avvikelser <b>synliga</b> i loggströmmen.
///
/// <para>
/// Lärdomen av det jämförbara intrånget: de upptäckte det först efter två dygn, när servern
/// dog. Uppetidspingen (<c>DRIFTOVERVAKNING.md</c>) larmar bara när servern är <em>nere</em> —
/// en attack som håller sig under den tröskeln, som ett ihållande gissande mot inloggningen,
/// är osynlig om den inte lämnar ett spår i loggen. Den här sänkan är det spåret: ett
/// strukturerat varningsevent per avvikelse som ett loggbaserat larm kan räkna och slå på.
/// </para>
///
/// <para>
/// Sänkan är avsiktligt smal (§KM.10): den bär aldrig e-post, barnnamn eller fritext — bara
/// metod och sökväg, det som säger <em>vad</em> som angrips utan att röja <em>vem</em>.
/// </para>
/// </summary>
public interface ISecurityEventSink
{
    /// <summary>
    /// En request avvisades av rate-limitern (§KM.0 A1). Ett enstaka avslag är ofarligt; en
    /// spik av dem mot samma sökväg är en attacksignal värd ett larm.
    /// </summary>
    public void RateLimitRejected(string method, string path);
}
