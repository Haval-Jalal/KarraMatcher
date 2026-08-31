namespace KarraMatcher.Application.Abstractions.Email;

/// <summary>
/// Skickar ett mejl.
///
/// <para>
/// Interfacet ligger här och implementationen i Infrastructure, av ett skäl som är mer än
/// prydlighet: överföringen till leverantören vilar på Data Privacy Framework, som är
/// under prövning i EU-domstolen (se känd risk i handoff-filen). Faller den ska
/// leverantören gå att byta utan att inloggningsflödet rörs.
/// </para>
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Skickar mejlet. Kastar inte vid leveransfel — inloggningen får inte falla för att
    /// en leverantör krånglar, och den som begärt koden får ändå samma svar.
    /// </summary>
    public Task SendAsync(
        string recipient,
        string subject,
        string body,
        CancellationToken cancellationToken);
}
