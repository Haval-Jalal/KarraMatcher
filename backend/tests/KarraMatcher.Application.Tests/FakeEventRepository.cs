using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Domain.Events;

namespace KarraMatcher.Application.Tests;

/// <summary>Handskriven attrapp, av samma skäl som <see cref="FakeTeamRepository"/>.</summary>
internal sealed class FakeEventRepository : IEventRepository
{
    public List<Event> Matches { get; } = [];

    public Task<Event?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Matches.FirstOrDefault(match => match.Id == id));

    public Task<IReadOnlyList<Event>> ListByTruppAsync(
        Guid ageGroupId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Event>>(
            [.. Matches.Where(match => match.AgeGroupId == ageGroupId).OrderBy(match => match.KickoffUtc)]);
}
