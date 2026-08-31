namespace KarraMatcher.Application.Abstractions.Security;

/// <summary>
/// Utfärdar den kortlivade access-token som klienten skickar i <c>Authorization</c>.
///
/// <para>
/// Interfacet ligger här och implementationen i Infrastructure, eftersom signering är en
/// teknikdetalj: byter vi format eller nyckelhantering ska inget användningsfall märka det.
/// </para>
/// </summary>
public interface IAccessTokenIssuer
{
    /// <summary>
    /// Signerar en token för kontot och svarar med den och dess utgångstid.
    ///
    /// <para>
    /// Rollerna följer med in i token, så att varje efterföljande anrop kan avgöras utan
    /// en databasfråga. Priset är att en ändrad roll slår igenom först när token förnyas
    /// — som mest en kvart. Att återkalla omedelbart kräver i stället att sessionen
    /// avslutas, vilket är vad <c>SignOut</c> gör.
    /// </para>
    /// </summary>
    public (string Token, DateTime ExpiresUtc) Issue(
        Guid accountId,
        string email,
        Features.Auth.AccountRoles roles);
}
