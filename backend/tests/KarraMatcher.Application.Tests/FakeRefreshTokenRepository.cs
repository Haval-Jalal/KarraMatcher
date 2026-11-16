using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Refresh-tokens i en lista.
///
/// <para>
/// Bevarar det som betyder något för testerna: uppslagning sker på hash, och en token som
/// är ersatt eller återkallad hittas <em>ändå</em>. Ett fake som filtrerade bort dem hade
/// gjort återanvändningsdetekteringen omöjlig att pröva — och testerna hade gått gröna.
/// </para>
/// </summary>
internal sealed class FakeRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly List<RefreshToken> _tokens = [];

    public IReadOnlyList<RefreshToken> Tokens => _tokens;

    public int SaveCount { get; private set; }

    /// <summary>
    /// När den är satt låter nästa <see cref="TrySaveChangesAsync"/> som om en samtidig förnyelse
    /// hann före (optimistisk concurrency): den svarar <c>false</c> en gång och nollställs sedan.
    /// </summary>
    public bool NextSaveLosesRace { get; set; }

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(_tokens.FirstOrDefault(t => t.TokenHash == tokenHash));

    public Task AddAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        _tokens.Add(token);

        return Task.CompletedTask;
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        foreach (var token in _tokens.Where(t => t.FamilyId == familyId && t.RevokedUtc is null))
        {
            token.RevokedUtc = nowUtc;
        }

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;

        return Task.CompletedTask;
    }

    public Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        if (NextSaveLosesRace)
        {
            NextSaveLosesRace = false;

            return Task.FromResult(false);
        }

        SaveCount++;

        return Task.FromResult(true);
    }
}
