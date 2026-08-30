using System.Security.Claims;
using System.Text;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace KarraMatcher.Infrastructure.Security;

/// <summary>
/// Signerar access-tokens med HMAC-SHA256.
///
/// <para>
/// Symmetrisk signering räcker och är rätt här: det finns en enda utfärdare och en enda
/// mottagare, båda våra. Asymmetriska nycklar löser problemet att någon <em>annan</em>
/// ska kunna verifiera utan att kunna signera, och det problemet har vi inte.
/// </para>
///
/// <para>
/// Vad som ligger i token: konto-id och adress. Adressen finns med för att gränssnittet
/// ska kunna visa vem som är inloggad utan ett extra anrop. <b>Ingenting om barn</b> —
/// en token skickas med varje request och hamnar i mellanlager vi inte styr över.
/// </para>
/// </summary>
internal sealed class JwtAccessTokenIssuer(
    IOptions<AuthOptions> options,
    TimeProvider clock) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string Token, DateTime ExpiresUtc) Issue(Guid accountId, string email)
    {
        var settings = options.Value;
        var now = clock.GetUtcNow().UtcDateTime;
        var expires = now.Add(settings.AccessTokenLifetime);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),

                // Egen identitet per token. Gör en enskild token spårbar i loggar utan
                // att någon personuppgift behöver loggas (§KM.10).
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (_handler.CreateToken(descriptor), expires);
    }
}
