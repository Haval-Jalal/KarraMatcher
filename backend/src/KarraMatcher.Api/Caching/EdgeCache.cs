using System.Globalization;
using System.Security.Cryptography;

using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace KarraMatcher.Api.Caching;

/// <summary>
/// Cache-headers för Vercels edge (§KM.11).
///
/// <para>
/// Render free somnar efter ca 15 minuters tystnad och tar omkring 50 sekunder att vakna.
/// Appen används mest lördag morgon, efter en tyst natt — den första föräldern som öppnar
/// schemat skulle alltså få vänta. Motmedlet är att publika GET-svar bär
/// <c>s-maxage</c>, så att edge svarar utan att backend väcks alls.
/// </para>
///
/// <para>
/// Standarden är den säkra: <c>private, no-store</c> på allt. En endpoint blir cachebar
/// bara genom att uttryckligen säga det med <see cref="WithEdgeCache"/>. Att glömma
/// markeringen kostar prestanda; motsatt standard hade kunnat kosta en användares data.
/// </para>
/// </summary>
public static class EdgeCache
{
    internal const string PrivateHeaderValue = "private, no-store";

    public static IServiceCollection AddKarraEdgeCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<EdgeCacheOptions>()
            .Bind(configuration.GetSection(EdgeCacheOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Markerar en endpoint som publikt cachebar. Svaret får då <c>Cache-Control</c> med
    /// <c>s-maxage</c>, en ETag, och besvarar <c>If-None-Match</c> med <c>304</c>.
    /// </summary>
    public static TBuilder WithEdgeCache<TBuilder>(this TBuilder builder, EdgeCacheProfile profile)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Add(endpoint => endpoint.Metadata.Add(new EdgeCacheAttribute(profile)));
        return builder;
    }

    public static IApplicationBuilder UseKarraEdgeCache(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices
                .GetRequiredService<IOptions<EdgeCacheOptions>>()
                .Value;

            var metadata = context.GetEndpoint()?.Metadata.GetMetadata<EdgeCacheAttribute>();

            if (metadata is null || !IsCacheableRequest(context.Request))
            {
                MarkPrivate(context);
                await next(context).ConfigureAwait(false);
                return;
            }

            await ServeCacheableAsync(context, next, metadata.Profile, options).ConfigureAwait(false);
        });
    }

    /// <summary>Endast GET och HEAD kan cachas meningsfullt.</summary>
    private static bool IsCacheableRequest(HttpRequest request) =>
        HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method);

    private static void MarkPrivate(HttpContext context) =>
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state;

            // Skriver inte över en handler som redan sagt sitt.
            if (string.IsNullOrEmpty(response.Headers.CacheControl))
            {
                response.Headers.CacheControl = PrivateHeaderValue;
            }

            return Task.CompletedTask;
        }, context.Response);

    /// <summary>
    /// Buffrar svaret för att kunna räkna fram en ETag, sätter headers och svarar
    /// <c>304</c> när klienten redan har rätt version.
    ///
    /// <para>
    /// Buffringen sker bara för endpoints som uttryckligen markerats cachebara. De är
    /// små listor och kalenderfiler, inte strömmande innehåll — kostnaden är känd och
    /// begränsad, till skillnad från att buffra hela API:t.
    /// </para>
    /// </summary>
    private static async Task ServeCacheableAsync(
        HttpContext context,
        RequestDelegate next,
        EdgeCacheProfile profile,
        EdgeCacheOptions options)
    {
        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var payload = buffer.ToArray();

        // Bara lyckade svar cachas. Ett fel ska aldrig ligga kvar på edge, och ett svar
        // som sätter en cookie är per definition användarspecifikt (§KM.3).
        if (context.Response.StatusCode != StatusCodes.Status200OK
            || context.Response.Headers.ContainsKey(HeaderNames.SetCookie))
        {
            if (string.IsNullOrEmpty(context.Response.Headers.CacheControl))
            {
                context.Response.Headers.CacheControl = PrivateHeaderValue;
            }

            await WriteAsync(context, originalBody, payload).ConfigureAwait(false);
            return;
        }

        var etag = ComputeETag(payload);

        context.Response.Headers.CacheControl = BuildHeaderValue(profile, options);
        context.Response.Headers.ETag = etag;

        if (MatchesClientVersion(context.Request, etag))
        {
            context.Response.StatusCode = StatusCodes.Status304NotModified;
            context.Response.ContentLength = null;
            context.Response.Headers.Remove(HeaderNames.ContentType);
            return;
        }

        await WriteAsync(context, originalBody, payload).ConfigureAwait(false);
    }

    private static async Task WriteAsync(HttpContext context, Stream destination, byte[] payload)
    {
        context.Response.ContentLength = payload.Length;
        await destination.WriteAsync(payload, context.RequestAborted).ConfigureAwait(false);
    }

    internal static string BuildHeaderValue(EdgeCacheProfile profile, EdgeCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // max-age=0 med s-maxage: webbläsaren frågar varje gång, edge svarar utan att
        // väcka Render. Det är edge-träffen som är poängen, inte webbläsarens cache —
        // en match som flyttas ska inte ligga kvar i en telefon.
        return string.Create(
            CultureInfo.InvariantCulture,
            $"public, max-age=0, s-maxage={options.SecondsFor(profile)}, "
                + $"stale-while-revalidate={options.StaleWhileRevalidateSeconds}");
    }

    /// <summary>Stark ETag ur svarets innehåll. Samma bytes ger alltid samma tagg.</summary>
    internal static string ComputeETag(byte[] payload)
    {
        var hash = SHA256.HashData(payload ?? []);
        return string.Create(CultureInfo.InvariantCulture, $"\"{Convert.ToHexString(hash)[..32]}\"");
    }

    /// <summary>
    /// Sant om klienten redan har svaret. Hanterar flera taggar, <c>*</c>, och det
    /// <c>W/</c>-prefix en mellanliggande cache kan ha lagt på.
    ///
    /// <para>
    /// <c>W/</c>-hanteringen är inte teoretisk. Verifierat i skarp drift 2026-08-30:
    /// Vercels edge försvagar vår starka ETag till <c>W/"…"</c> på vägen ut, eftersom den
    /// kan komprimera svaret. Klienten skickar sedan tillbaka den försvagade taggen.
    /// Utan prefixhanteringen slutar villkorade anrop fungera i drift — och inget test i
    /// CI skulle märka det, eftersom ingen edge finns där.
    /// </para>
    /// </summary>
    internal static bool MatchesClientVersion(HttpRequest request, string etag)
    {
        ArgumentNullException.ThrowIfNull(request);

        var header = request.Headers.IfNoneMatch;

        if (header.Count == 0)
        {
            return false;
        }

        foreach (var value in header)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (var candidate in value.Split(','))
            {
                var trimmed = candidate.Trim();

                if (trimmed == "*")
                {
                    return true;
                }

                if (trimmed.StartsWith("W/", StringComparison.Ordinal))
                {
                    trimmed = trimmed[2..];
                }

                if (string.Equals(trimmed, etag, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
