namespace KarraMatcher.Application.Features.Auth;

/// <summary>
/// En nyutfärdad session.
///
/// <para>
/// <see cref="RefreshToken"/> är klartexten och finns bara i det här ögonblicket — den
/// lämnar servern som en <c>httpOnly</c>-cookie och lagras aldrig, bara som hash
/// (<see cref="Domain.Accounts.RefreshToken.TokenHash"/>). Skriv den aldrig i en logg
/// och aldrig i ett JSON-svar.
/// </para>
/// </summary>
public sealed record SessionTokens(
    string AccessToken,
    DateTime AccessExpiresUtc,
    string RefreshToken,
    DateTime RefreshExpiresUtc);
