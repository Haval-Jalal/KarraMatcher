using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Matches;

namespace KarraMatcher.Application.Tests;

/// <summary>Handskriven attrapp, av samma skäl som <see cref="FakeTeamRepository"/>.</summary>
internal sealed class FakeMatchRepository : IMatchRepository
{
    public List<Match> Matches { get; } = [];

    public Task<Match?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Matches.FirstOrDefault(match => match.Id == id));
}
