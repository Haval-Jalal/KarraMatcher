using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Stänger direktåtkomsten till Render-backenden (§KM.11, checklista 4.6, <c>#584</c>).
///
/// <para>
/// Render-URL:en är publikt nåbar — <c>vercel.ts</c> proxar <c>/api</c> dit. Utan den här grinden
/// kan en angripare som går förbi Vercel förfalska en ny <c>X-Forwarded-For</c> per anrop och få
/// en egen rate-limit-hink varje gång, inklusive mot inloggningen. Rate-limitern partitionerar på
/// klientens IP ur just den headern (se <see cref="RateLimiting"/>), så dess värde vilar på att
/// trafiken verkligen kom via proxyn.
/// </para>
///
/// <para>
/// Mekanismen är issuets förstahandsförslag: en <b>delad hemlig header</b>. Vercels edge sätter
/// <see cref="HeaderName"/> på varje vidarebefordrad request (<c>requestHeaders</c> i
/// <c>vercel.ts</c>, värdet ur Vercel-varen <c>KARRA_EDGE_AUTH</c>); backend kräver den och avvisar
/// allt annat med <c>403</c> innan rate-limitern ens hinner partitionera. IP-allowlist eller privat
/// nät är uteslutet på gratisstacken — Vercels edge har ingen fast utgående IP — så en hemlighet är
/// rätt kontroll här.
/// </para>
///
/// <para>
/// <b>Inert tills den konfigureras.</b> Är <see cref="EdgeAuthOptions.SharedSecret"/> tom läggs
/// grinden inte ens in i pipelinen. Det gör utrullningen ofarlig i den här ordningen, där varje steg
/// är bakåtkompatibelt och prod aldrig kan gå sönder mitt i:
/// <list type="number">
///   <item>Merga — allt inert, inget ändras.</item>
///   <item>Sätt <c>KARRA_EDGE_AUTH</c> i Vercel — headern skickas nu, men backend struntar i den.</item>
///   <item>Sätt samma värde som <c>EdgeAuth__SharedSecret</c> i Render — grinden aktiveras.</item>
/// </list>
/// </para>
///
/// <para>
/// <c>/health</c> är undantaget: uppetidspingen (§KM.11) når Render direkt och bär ingen header.
/// Den ytan röjer inga uppgifter och är ofarlig att lämna öppen.
/// </para>
/// </summary>
public static class EdgeAuthentication
{
    /// <summary>
    /// Headern Vercels edge sätter på varje vidarebefordrad request, och som backend kräver.
    /// Loggas aldrig med sitt värde (§KM.10) — bara dess frånvaro, via <see cref="ISecurityEventSink"/>.
    /// </summary>
    public const string HeaderName = "X-Karra-Edge-Auth";

    /// <summary>Config-sektionen som <see cref="EdgeAuthOptions"/> binds från.</summary>
    public const string SectionName = "EdgeAuth";

    public static IServiceCollection AddKarraEdgeAuth(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<EdgeAuthOptions>(configuration.GetSection(SectionName));
        return services;
    }

    public static IApplicationBuilder UseKarraEdgeAuth(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var secret = app.ApplicationServices
            .GetRequiredService<IOptions<EdgeAuthOptions>>().Value.SharedSecret;

        // Ingen hemlighet konfigurerad -> grinden avstängd. Att inte ens lägga in mellanvaran
        // håller den helt kostnadsfri (och bevisligt inert) tills den rullas ut i drift.
        if (string.IsNullOrEmpty(secret))
        {
            return app;
        }

        var expected = Encoding.UTF8.GetBytes(secret);

        return app.Use(async (context, next) =>
        {
            // Uppetidspingen når Render direkt (§KM.11) och bär ingen edge-header. /health och
            // /health/ready måste därför förbli öppna — de röjer inga uppgifter.
            if (context.Request.Path.StartsWithSegments("/health"))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (CameThroughEdge(context, expected))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            // Hit når bara en request som gick förbi Vercel. Lämna ett spår (metod + redigerad
            // sökväg, aldrig headern själv — §KM.10) och avvisa innan rate-limitern och auth.
            context.RequestServices.GetService<ISecurityEventSink>()
                ?.DirectAccessRejected(
                    context.Request.Method,
                    RequestLogRedaction.Redact(context.Request.Path.Value ?? "/"));

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        });
    }

    private static bool CameThroughEdge(HttpContext context, byte[] expected)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out StringValues provided))
        {
            return false;
        }

        // Konstant-tidsjämförelse så en angripare inte kan gissa hemligheten tecken för tecken
        // på svarstiden. Olika längd ger false direkt, vilket är acceptabelt — längden på en
        // slumpad hemlighet är inte det känsliga.
        var candidate = Encoding.UTF8.GetBytes(provided.ToString());
        return CryptographicOperations.FixedTimeEquals(candidate, expected);
    }
}
