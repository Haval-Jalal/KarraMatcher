using KarraMatcher.Application.Abstractions.Diagnostics;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Läser requestens correlation-id ur <see cref="HttpContext.TraceIdentifier"/> — samma värde
/// som <see cref="CorrelationIdMiddleware"/> satt och som loggarna bär.
///
/// <para>
/// Det är här HTTP möter de HTTP-omedvetna lagren: audit-loggen ber om
/// <see cref="ICorrelationContext.CorrelationId"/> och får requestens id utan att veta varifrån.
/// Utanför en request (t.ex. ett bakgrundsjobb) finns ingen <c>HttpContext</c> och id:t är tomt.
/// </para>
/// </summary>
internal sealed class HttpCorrelationContext(IHttpContextAccessor accessor) : ICorrelationContext
{
    public string? CorrelationId => accessor.HttpContext?.TraceIdentifier;
}
