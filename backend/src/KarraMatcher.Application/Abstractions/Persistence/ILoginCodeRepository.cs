using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Abstractions.Persistence;

public interface ILoginCodeRepository
{
    /// <summary>
    /// Den senast utfärdade koden för adressen, oavsett om den går att använda.
    ///
    /// <para>
    /// Även förbrukade och utgångna koder, av samma skäl som för refresh-tokens: att
    /// skilja "finns inte" från "får inte användas" är hela grunden för spärren.
    /// </para>
    /// </summary>
    public Task<LoginCode?> FindLatestAsync(string email, CancellationToken cancellationToken);

    public Task AddAsync(LoginCode code, CancellationToken cancellationToken);

    /// <summary>Ogiltigförklarar alla oanvända koder för adressen.</summary>
    public Task ConsumeOutstandingAsync(string email, DateTime nowUtc, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);
}
