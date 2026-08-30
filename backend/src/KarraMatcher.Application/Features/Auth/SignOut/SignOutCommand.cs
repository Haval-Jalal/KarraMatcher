using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.SignOut;

/// <summary>Avslutar sessionen och återkallar hela dess familj.</summary>
public sealed record SignOutCommand(string? RefreshToken) : ICommand<Unit>;
