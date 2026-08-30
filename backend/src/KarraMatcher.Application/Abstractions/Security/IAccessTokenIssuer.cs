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
    /// <summary>Signerar en token för kontot och svarar med den och dess utgångstid.</summary>
    public (string Token, DateTime ExpiresUtc) Issue(Guid accountId, string email);
}
