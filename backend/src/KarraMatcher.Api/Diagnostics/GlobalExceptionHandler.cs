using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Fångar allt som inte hanterats och svarar med ProblemDetails (RFC 7807).
/// Inga stack traces eller interna meddelanden lämnar servern — se
/// SAKERHET-CHECKLISTA rad 6.6.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // Detaljerna loggas server-side tillsammans med correlation-ID,
        // men skickas aldrig till klienten.
        LogUnhandled(logger, httpContext.Request.Method, httpContext.Request.Path, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Något gick fel",
                Detail = "Ett oväntat fel inträffade. Försök igen om en stund.",
            },
        });
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Error,
        Message = "Ohanterat fel under {Method} {Path}")]
    private static partial void LogUnhandled(
        ILogger logger, string method, string path, Exception exception);
}
