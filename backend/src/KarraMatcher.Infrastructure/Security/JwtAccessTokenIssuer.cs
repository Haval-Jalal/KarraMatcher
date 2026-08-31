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

    public (string Token, DateTime ExpiresUtc) Issue(
        Guid accountId,
        string email,
        AccountRoles roles)
    {
        ArgumentNullException.ThrowIfNull(roles);

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
            Subject = new ClaimsIdentity(Claims(accountId, email, roles)),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        return (_handler.CreateToken(descriptor), expires);
    }

    /// <summary>
    /// Anspråken i en token.
    ///
    /// <para>
    /// Ett anspråk per lag en tränare ansvarar för. Alternativet — en kommaseparerad
    /// sträng — hade krävt att varje kontroll delade upp den igen, och en sådan
    /// uppdelning är precis där ett tomt värde eller en extra kommatecken blir en
    /// behörighet ingen avsett.
    /// </para>
    /// </summary>
    private static IEnumerable<Claim> Claims(Guid accountId, string email, AccountRoles roles)
    {
        yield return new Claim(JwtRegisteredClaimNames.Sub, accountId.ToString());
        yield return new Claim(JwtRegisteredClaimNames.Email, email);

        // Egen identitet per token. Gör en enskild token spårbar i loggar utan att någon
        // personuppgift behöver loggas (§KM.10).
        yield return new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString());

        if (roles.IsAdmin)
        {
            yield return new Claim(ClaimTypes.Role, AuthClaims.AdminRole);
        }

        foreach (var slug in roles.CoachOf)
        {
            yield return new Claim(AuthClaims.Coach, slug);
        }
    }
}
