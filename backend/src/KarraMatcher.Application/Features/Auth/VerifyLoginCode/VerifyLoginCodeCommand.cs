using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.VerifyLoginCode;

/// <summary>Verifierar en engångskod och ger en session om den stämmer.</summary>
public sealed record VerifyLoginCodeCommand(string Email, string Code) : ICommand<SessionTokens?>;
