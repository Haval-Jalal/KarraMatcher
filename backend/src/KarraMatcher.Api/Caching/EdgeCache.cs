using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace KarraMatcher.Api.Caching;

/// <summary>
/// Cache-headers för Vercels edge (§KM.11).
///
/// <para>
/// <b>Stängd app i v2 (§KM.3, `#191`):</b> varje svar bär någons uppgifter — inget är
/// publikt längre. Därför finns bara en regel kvar: <c>private, no-store</c> på allt. Den
/// publika edge-cachningen (s-maxage, ETag, ICS-feeden) togs bort när schemat slutade vara
/// öppet. Att glömma markera en endpoint kan inte längre kosta en användares data, eftersom
/// det inte finns någon markering att glömma.
/// </para>
///
/// <para>
/// Mellanvaran skriver aldrig över en handler som redan satt <c>Cache-Control</c> själv:
/// ASP.NET:s health check-middleware sätter t.ex. sitt eget <c>no-store, no-cache</c>, och
/// pingen från uppetidsverktyget ska aldrig få ett cachat svar som döljer en nere backend.
/// </para>
/// </summary>
public static class EdgeCache
{
    internal const string PrivateHeaderValue = "private, no-store";

    public static IApplicationBuilder UseKarraEdgeCache(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            MarkPrivate(context);
            await next(context).ConfigureAwait(false);
        });
    }

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
}
