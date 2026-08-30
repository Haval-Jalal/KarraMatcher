using KarraMatcher.Application.Abstractions.Messaging;

namespace KarraMatcher.Application.Features.Auth.RefreshSession;

/// <summary>
/// Byter refresh-token mot en ny session.
///
/// <para>
/// Token kommer ur cookien och aldrig ur en body — API-lagret plockar ut den. Att den
/// ändå passerar som parameter håller användningsfallet oberoende av HTTP.
/// </para>
/// </summary>
public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<SessionTokens?>;
