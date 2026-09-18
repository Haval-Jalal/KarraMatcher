using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Events.GetEvent;

/// <summary>En enskild händelse. Returnerar null om den inte finns.</summary>
public sealed record GetEventQuery(Guid Id) : IQuery<EventDetailDto?>;
