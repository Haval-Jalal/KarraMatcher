using KarraMatcher.Application.Features.Attendance;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KarraMatcher.Api.Features.Attendance;

/// <summary>
/// Stänger en endpoint när kallelsen är avslagen för laget (§KM.7).
///
/// <h3>Vad den svarar</h3>
///
/// <para>
/// <c>404</c>, inte <c>403</c>. Ett <c>403</c> säger "funktionen finns, men inte för dig"
/// — och då går det att kartlägga vilka lag som har kallelsen påslagen genom att prova sig
/// fram. Ett <c>404</c> säger ingenting alls, vilket är rätt svar för något som inte är
/// släppt.
/// </para>
///
/// <h3>Laget hämtas ur adressen</h3>
///
/// <para>
/// Antingen <c>slug</c> eller <c>matchId</c>, beroende på vilken av dem routen bär. Det
/// finns inget lagfält att skicka i kroppen, så en anropare kan inte peka grinden mot ett
/// annat lag än det hen faktiskt anropar.
/// </para>
///
/// <para>
/// Saknas båda är det ett programmeringsfel, inte ett anroparfel. Då stängs endpointen —
/// en grind som inte vet vad den vaktar ska vara stängd, inte öppen.
/// </para>
///
/// <h3>Attributet ersätter inte kontrollen i handlern</h3>
///
/// <para>
/// Det vaktar routen. <see cref="AttendanceGate"/> vaktar användningsfallet, för en handler
/// kan anropas från något annat än en controller. Att båda finns är avsiktligt (§KM.7), och
/// ett test fäller bygget om en närvaro-endpoint saknar det här attributet.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireAttendanceEnabledAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var gate = context.HttpContext.RequestServices.GetRequiredService<AttendanceGate>();
        var cancellationToken = context.HttpContext.RequestAborted;

        var enabled = await IsEnabledAsync(context, gate, cancellationToken).ConfigureAwait(false);

        if (!enabled)
        {
            context.Result = NotFound(context);

            return;
        }

        await next().ConfigureAwait(false);
    }

    private static Task<bool> IsEnabledAsync(
        ActionExecutingContext context,
        AttendanceGate gate,
        CancellationToken cancellationToken)
    {
        var route = context.RouteData.Values;

        if (route.TryGetValue("slug", out var slug) && slug is string text && text.Length > 0)
        {
            return gate.IsEnabledForTeamAsync(text, cancellationToken);
        }

        if (route.TryGetValue("matchId", out var raw)
            && Guid.TryParse(raw?.ToString(), out var matchId))
        {
            return gate.IsEnabledForMatchAsync(matchId, cancellationToken);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Samma svar som för en adress som inte finns — samma ord, samma statuskod.
    /// </summary>
    private static ObjectResult NotFound(ActionExecutingContext context) =>
        new(new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Sidan finns inte",
            Detail = "Kontrollera adressen.",
            Instance = context.HttpContext.Request.Path,
        })
        {
            StatusCode = StatusCodes.Status404NotFound,
        };
}
