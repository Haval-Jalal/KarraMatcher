using Microsoft.AspNetCore.Http;

using Serilog.AspNetCore;
using Serilog.Events;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Håller kalender-nyckeln utanför loggarna (§KM.10).
///
/// <para>
/// Kalender-feeden nås på <c>/api/v1/kalender/{token}.ics</c>, där nyckeln <em>är</em>
/// behörigheten (§KM.4) — en 256-bitars bärar-token med stående läsrätt tills den återkallas.
/// En kalender-app pollar feeden om och om igen, så utan den här redigeringen hade nyckeln
/// hamnat i request-loggen (Serilog) och i felhanterarens rad vid varje poll. Nyckeln byts
/// därför mot en platshållare innan sökvägen loggas; de autentiserade vägarna (<c>min</c>,
/// <c>aterkalla</c>) bär ingen nyckel i sökvägen och rörs inte.
/// </para>
/// </summary>
public static class RequestLogRedaction
{
    private const string CalendarPrefix = "/api/v1/kalender/";
    private const string CalendarSuffix = ".ics";

    /// <summary>
    /// Kopplar in redigeringen i Serilogs request-loggning: samma fyra egenskaper som standard,
    /// men <c>RequestPath</c> går genom <see cref="Redact"/> först.
    /// </summary>
    public static void Configure(RequestLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.GetMessageTemplateProperties = (httpContext, requestPath, elapsedMs, statusCode) =>
        [
            new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
            new LogEventProperty("RequestPath", new ScalarValue(Redact(requestPath))),
            new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
            new LogEventProperty("Elapsed", new ScalarValue(elapsedMs)),
        ];
    }

    /// <summary>
    /// Byter ut kalender-nyckeln i en sökväg mot en platshållare. Andra sökvägar lämnas oförändrade.
    /// </summary>
    public static string Redact(string requestPath)
    {
        if (requestPath.StartsWith(CalendarPrefix, StringComparison.OrdinalIgnoreCase)
            && requestPath.EndsWith(CalendarSuffix, StringComparison.OrdinalIgnoreCase)
            && requestPath.Length > CalendarPrefix.Length + CalendarSuffix.Length)
        {
            return $"{CalendarPrefix}[redacted]{CalendarSuffix}";
        }

        return requestPath;
    }
}
