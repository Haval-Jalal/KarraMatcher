using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.RequestLoginCode;

/// <summary>Begär en engångskod till en adress. Svarar aldrig med om den fanns.</summary>
public sealed record RequestLoginCodeCommand(string Email) : ICommand<Unit>;
