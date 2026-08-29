using Serilog.Context;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Ger varje request ett correlation-ID som följer med genom alla loggrader och
/// returneras till klienten. Utan det går det inte att följa ett ärende end-to-end
/// när en förälder hör av sig om att något gick fel (CLAUDE.md → Loggning).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var correlationId = Resolve(context);
        context.TraceIdentifier = correlationId;

        // Headern sätts strax innan svaret skickas, inte nu. Anledningen: vid ett
        // ohanterat fel nollställer felhanteraren svaret och skulle annars radera
        // headern — på precis de svar där den behövs mest.
        context.Response.OnStarting(static state =>
        {
            var ctx = (HttpContext)state;
            ctx.Response.Headers[HeaderName] = ctx.TraceIdentifier;
            return Task.CompletedTask;
        }, context);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next(context);
        }
    }

    private static string Resolve(HttpContext context)
    {
        // Skickar klienten med ett eget id återanvänder vi det, så att en kedja av
        // anrop kan följas. Annars skapar vi ett nytt.
        if (context.Request.Headers.TryGetValue(HeaderName, out var incoming))
        {
            var candidate = incoming.ToString();
            if (IsSafe(candidate))
            {
                return candidate;
            }
        }

        return Guid.NewGuid().ToString("N");
    }

    // Ett id från klienten hamnar i en svarsheader och i loggarna. Vi begränsar det
    // därför hårt i längd och teckenuppsättning i stället för att lita på indata.
    private static bool IsSafe(string value) =>
        value.Length is > 0 and <= 64
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
