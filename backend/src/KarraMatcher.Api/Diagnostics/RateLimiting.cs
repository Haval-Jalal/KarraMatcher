using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Rate limiting på de publika endpointsen. Mallen lägger det under "vid behov",
/// men vi lyfter det till baslinje (§KM.0 A1) eftersom schema- och ICS-endpointsen
/// ligger oautentiserade på öppet internet.
///
/// Komplikationen är proxyn: all trafik når backend från Vercels edge (§KM.11).
/// En gräns per anslutnings-IP skulle lägga varenda förälder i samma hink och
/// strypa hela laget så fort någon laddade om sidan. Därför partitioneras det på
/// klientens IP från <c>X-Forwarded-For</c>.
/// </summary>
public static class RateLimiting
{
    public const string PermitKey = "RateLimiting:PermitPerMinute";

    /// <summary>
    /// Egen, hårdare gräns för inloggningen.
    ///
    /// <para>
    /// Den allmänna gränsen är satt för att ett helt lag ska kunna läsa schemat samtidigt
    /// och duger inte här: 120 försök i minuten mot en sexsiffrig kod är en helt annan
    /// sak än 120 sidvisningar. Spärren per kod stoppar gissningar mot <em>en</em> kod;
    /// den här stoppar den som begär nya koder i strid ström för att få fler försök.
    /// </para>
    /// </summary>
    public const string LoginPolicy = "inloggning";

    private const int LoginPermitPerMinute = 8;
    private const int DefaultPermitPerMinute = 120;

    /// <summary>
    /// Skyddsgränsen är satt högt nog att hela laget kan använda appen samtidigt,
    /// men lågt nog att en enskild angripare inte kan mätta tjänsten.
    /// </summary>
    private const int GlobalMultiplier = 20;

    public static IServiceCollection AddKarraRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var permitPerMinute =
            int.TryParse(configuration[PermitKey], CultureInfo.InvariantCulture, out var configured)
            && configured > 0
                ? configured
                : DefaultPermitPerMinute;

        // Vercel och Render sätter X-Forwarded-For. Utan det här ser vi bara proxyns
        // adress. Att rensa KnownProxies krävs eftersom proxyns IP inte är känd i
        // förväg — se den ärliga begränsningen i klassdokumentationen nedan.
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            options.ForwardLimit = 2;
        });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Två gränser i kedja. Den första per klient, den andra som skyddsnät.
            //
            // Skyddsnätet behövs eftersom X-Forwarded-For går att förfalska: Render-URL:en
            // är publikt nåbar, så en angripare som kringgår Vercel kan hitta på en ny
            // klientadress för varje anrop och därmed få en egen hink varje gång. Den
            // opartitionerade gränsen går inte att komma runt på det sättet.
            //
            // Den riktiga lösningen är att stänga direktåtkomsten till Render
            // (SAKERHET-CHECKLISTA rad 4.6, ännu öppen).
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext, string>(
                    context => RateLimitPartition.GetFixedWindowLimiter(
                        ClientKey(context),
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitPerMinute,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        })),
                PartitionedRateLimiter.Create<HttpContext, string>(
                    _ => RateLimitPartition.GetFixedWindowLimiter(
                        "alla",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitPerMinute * GlobalMultiplier,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        })));

            options.AddPolicy(LoginPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
                ClientKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = LoginPermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            options.OnRejected = (context, _) =>
            {
                // Retry-After krävs för att klienten ska veta hur länge den ska vänta
                // i stället för att hamra vidare.
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var value)
                    ? (int)value.TotalSeconds
                    : 60;

                context.HttpContext.Response.Headers.RetryAfter =
                    new StringValues(retryAfter.ToString(CultureInfo.InvariantCulture));

                return ValueTask.CompletedTask;
            };
        });

        return services;
    }

    /// <summary>
    /// Partitionsnyckel: klientens IP efter att forwarded headers tolkats.
    /// Saknas adress hamnar anropet i en gemensam hink — hellre det än ingen gräns alls.
    /// </summary>
    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "okand";
}
