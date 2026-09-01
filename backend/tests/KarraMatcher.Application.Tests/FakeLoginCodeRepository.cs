using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Accounts;

namespace KarraMatcher.Application.Tests;

/// <summary>Delas av inloggnings- och raderingstesterna.</summary>
internal sealed class FakeLoginCodeRepository : ILoginCodeRepository
{
    public List<LoginCode> Codes { get; } = [];

    public Task<LoginCode?> FindLatestAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(Codes
            .Where(c => c.Email == email)
            .OrderByDescending(c => c.CreatedUtc)
            .FirstOrDefault());

    public Task AddAsync(LoginCode code, CancellationToken cancellationToken)
    {
        Codes.Add(code);

        return Task.CompletedTask;
    }

    public Task ConsumeOutstandingAsync(
        string email,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        foreach (var code in Codes.Where(c => c.Email == email && c.ConsumedUtc is null))
        {
            code.ConsumedUtc = nowUtc;
        }

        return Task.CompletedTask;
    }

    public Task DeleteForEmailAsync(string email, CancellationToken cancellationToken)
    {
        Codes.RemoveAll(c => c.Email == email);

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
