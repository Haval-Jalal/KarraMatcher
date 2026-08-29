using FluentValidation;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Översätter ett valideringsfel till <c>400</c> med ProblemDetails.
///
/// <para>
/// Registreras <em>före</em> <see cref="GlobalExceptionHandler"/>, som annars hade gjort
/// felaktig indata till ett <c>500</c> — och en användares stavfel är inte ett serverfel.
/// </para>
///
/// <para>
/// Valideringsmeddelandena är skrivna för att visas (§KM.9, svenska) och innehåller aldrig
/// annat än det anroparen själv skickade in. Att de når klienten är avsikten, till skillnad
/// från interna feldetaljer.
/// </para>
/// </summary>
public sealed class ValidationExceptionHandler(IProblemDetailsService problemDetailsService)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var errors = validationException.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Felaktig förfrågan",
            },
        }).ConfigureAwait(false);
    }
}
