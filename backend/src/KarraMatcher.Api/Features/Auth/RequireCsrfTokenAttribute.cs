using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace KarraMatcher.Api.Features.Auth;

/// <summary>
/// Kräver en giltig anti-forgery-token i <c>X-CSRF-TOKEN</c>.
///
/// <para>
/// Ramverkets egen <c>[ValidateAntiForgeryToken]</c> ligger i MVC:s vy-maskineri och
/// registreras bara av <c>AddControllersWithViews()</c>. Det här är ett JSON-API som
/// använder <c>AddControllers()</c>, så attributet kastar i stället för att skydda —
/// vilket ser ut som ett 500 utan förklaring. Att dra in hela Razor för ett filter vore
/// fel väg; det här är samma kontroll utan resten.
/// </para>
///
/// <para>
/// Deklarativt och inte ett anrop inuti varje action, eftersom ett anrop är något man kan
/// glömma. Ett test kontrollerar att varje endpoint som ändrar tillstånd bär attributet.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
internal sealed class RequireCsrfTokenAttribute : Attribute, IFilterFactory
{
    public bool IsReusable => true;

    public IFilterMetadata CreateInstance(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        return new Filter(
            (IAntiforgery)serviceProvider.GetService(typeof(IAntiforgery))!,
            (ProblemDetailsFactory)serviceProvider.GetService(typeof(ProblemDetailsFactory))!);
    }

    private sealed class Filter(IAntiforgery antiforgery, ProblemDetailsFactory problems)
        : IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            try
            {
                await antiforgery.ValidateRequestAsync(context.HttpContext).ConfigureAwait(false);
            }
            catch (AntiforgeryValidationException)
            {
                // ProblemDetails och inte ett tomt 400: en klient som saknar token ska
                // kunna läsa varför, och hämta en ny i stället för att gissa.
                var details = problems.CreateProblemDetails(
                    context.HttpContext,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Saknad eller ogiltig CSRF-token",
                    detail: "Hämta en ny token från /api/v1/auth/csrf och skicka den i "
                        + AuthenticationSetup.CsrfHeaderName + ".");

                context.Result = new ObjectResult(details)
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                };
            }
        }
    }
}
