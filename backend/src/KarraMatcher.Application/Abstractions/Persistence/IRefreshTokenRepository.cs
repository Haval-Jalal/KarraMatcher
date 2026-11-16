using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Abstractions.Persistence;

public interface IRefreshTokenRepository
{
    /// <summary>
    /// Slår upp en token på dess hash, med kontot laddat.
    ///
    /// <para>
    /// Svarar även för en token som är ersatt, återkallad eller utgången. Att skilja
    /// "finns inte" från "får inte användas" är hela grunden för
    /// återanvändningsdetekteringen — en uppslagning som filtrerade bort de förbrukade
    /// hade gjort en stulen token oskiljbar från en påhittad.
    /// </para>
    /// </summary>
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken);

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken);

    /// <summary>
    /// Återkallar varje token i familjen som inte redan är återkallad.
    ///
    /// <para>
    /// Används både vid utloggning och när en förbrukad token dyker upp igen. I det senare
    /// fallet är det just familjen som ska falla: tjuven har nästa token i kedjan, inte
    /// den som redan använts.
    /// </para>
    /// </summary>
    public Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Sparar ändringarna men svarar <c>false</c> i stället för att kasta om en annan,
    /// samtidig transaktion redan hann ändra samma rad (optimistisk concurrency).
    ///
    /// <para>
    /// Rotationen använder den här i stället för <see cref="SaveChangesAsync"/>: om två
    /// förnyelser med samma token racar ska bara en få markera den ersatt. Den som förlorar
    /// loppet får <c>false</c> och svarar <c>null</c> uppåt — ingen familjesplittring, ingen
    /// återkallning, det är inte en stöld (`#611`). Concurrency-detaljen (EF:s undantag) hålls
    /// här i infrastrukturlagret; Application ser bara utfallet.
    /// </para>
    /// </summary>
    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken);
}
