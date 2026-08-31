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
}
