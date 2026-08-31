using System.Text;

using KarraMatcher.Application.Features.Auth;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace KarraMatcher.Api.Features.Auth;

/// <summary>
/// Kopplar in autentisering, auktorisering och CSRF-skydd.
///
/// <para>
/// <b>Ingen fallback-policy.</b> Det är den enda raden i hela uppsättningen som hade
/// kunnat låsa den publika delen med en enda ändring, och den vanligaste orsaken till att
/// en app plötsligt kräver inloggning för att se ett schema. Varje skyddad endpoint säger
/// det själv i stället (§KM.3). Ett test i <c>GuestAccessTests</c> fäller bygget om någon
/// inför en fallback ändå.
/// </para>
/// </summary>
internal static class AuthenticationSetup
{
    /// <summary>Klienten skickar tillbaka anti-forgery-token i den här headern.</summary>
    public const string CsrfHeaderName = "X-CSRF-TOKEN";

    /// <summary>
    /// Hur strikt anti-forgery-cookien kräver HTTPS.
    ///
    /// <para>
    /// I drift alltid <c>Always</c>: Render avslutar TLS och skickar
    /// <c>X-Forwarded-Proto</c>, och eftersom <c>UseForwardedHeaders</c> körs först av
    /// allt är anropet HTTPS när antiforgery tittar. Skulle den kedjan gå sönder ska
    /// cookien vägra sättas, inte tyst skickas i klartext.
    /// </para>
    ///
    /// <para>
    /// Lokalt och i tester går anropen över HTTP, och <c>Always</c> gör då att
    /// antiforgery <em>kastar</em> — inte att den hoppar över attributet. Det tog en
    /// felsökning att upptäcka, eftersom felet syns som ett 500 utan förklaring.
    /// </para>
    /// </summary>
    internal static CookieSecurePolicy SecurePolicyFor(bool isDevelopment) =>
        isDevelopment ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;

    public static IServiceCollection AddKarraAuthentication(
        this IServiceCollection services,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                /*
                 * Alla fyra kontrollerna pa: utfardare, mottagare, livstid och signatur
                 * (checklistan 1.3). Standardvardena har de flesta pa redan, men de star
                 * utskrivna har for att en avstangd kontroll ska krava att nagon aktivt
                 * skriver "false" -- inte att nagon glomt en rad.
                 */
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    // Standardvärdet är fem minuter. En utgången token ska vara utgången.
                    ClockSkew = TimeSpan.FromSeconds(30),
                };

                // Nyckeln och namnen läses först när tjänsterna byggts, eftersom
                // AuthOptions valideras vid start och inte finns här ännu.
                options.EventsType = null;
            });

        // Bindningen sker efter att optionsobjektet finns, så att nyckeln kommer från
        // samma validerade konfiguration som resten av inloggningen.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((jwt, auth) =>
            {
                var settings = auth.Value;

                jwt.TokenValidationParameters.ValidIssuer = settings.Issuer;
                jwt.TokenValidationParameters.ValidAudience = settings.Audience;
                jwt.TokenValidationParameters.IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));
            });

        // Policys ja, fallback nej. Se klassens kommentar: en fallback hade lagt krav pa
        // inloggning aven pa schemat, som ska vara oppet for vem som helst.
        services.AddAuthorization(options => options.AddKarraPolicies());

        // Kravet pa ratt lag laser routen, och behover darfor komma at anropet.
        services.AddHttpContextAccessor();
        services.AddSingleton<IAuthorizationHandler, CoachOfTeamHandler>();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = CsrfHeaderName;
            options.Cookie.Name = "karra_csrf";
            options.Cookie.SecurePolicy = SecurePolicyFor(environment.IsDevelopment());
            options.Cookie.SameSite = SameSiteMode.Lax;

            // Den här cookien ska läsas av klienten — den är halvan av double-submit och
            // bär ingen session. Det är refresh-cookien som är httpOnly.
            options.Cookie.HttpOnly = false;
        });

        return services;
    }
}
