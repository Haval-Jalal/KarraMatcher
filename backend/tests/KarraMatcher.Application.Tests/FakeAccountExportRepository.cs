using KarraMatcher.Application.Abstractions.Persistence;

namespace KarraMatcher.Application.Tests;

/// <summary>En export-läsning som testet matar med färdig data, eller null för okänt konto.</summary>
internal sealed class FakeAccountExportRepository : IAccountExportRepository
{
    private readonly Dictionary<Guid, AccountExportData> _data = [];

    public void Set(Guid accountId, AccountExportData data) => _data[accountId] = data;

    public Task<AccountExportData?> LoadForAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken) =>
        Task.FromResult(_data.TryGetValue(accountId, out var data) ? data : null);
}
