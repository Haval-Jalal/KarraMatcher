using KarraMatcher.Api.Diagnostics;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Hjälpare för att starta API:t med små justeringar per test, utan att
/// produktionskoden behöver känna till att den testas.
/// </summary>
public static class TestApp
{
    /// <summary>Lägger till en readiness-kontroll som alltid misslyckas.</summary>
    public static WebApplicationFactory<Program> WithFailingReadinessCheck(
        this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddHealthChecks().AddCheck(
                "trasigt-beroende",
                () => HealthCheckResult.Unhealthy("Simulerat fel"),
                tags: [HealthChecks.ReadyTag])));

    /// <summary>
    /// Kopplar in testets egen prov-endpoint bakom <c>[RequireAttendanceEnabled]</c>.
    ///
    /// <para>
    /// Kallelsens riktiga endpoints byggs i <c>#57</c>. Grinden måste ändå gå att pröva
    /// <em>nu</em> — en flagga som ingen provat är en flagga man upptäcker är trasig samma
    /// dag som funktionen släpps. Provrouten finns bara i tester och når aldrig drift.
    /// </para>
    /// </summary>
    public static WebApplicationFactory<Program> WithAttendanceProbe(
        this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddControllers().AddApplicationPart(typeof(AttendanceProbeController).Assembly)));

    /// <summary>
    /// Lägger en endpoint sist i kedjan som kastar. Den ligger efter
    /// <c>UseExceptionHandler</c>, vilket är hela poängen — annars testar vi inget.
    /// </summary>
    public static WebApplicationFactory<Program> WithThrowingEndpoint(
        this WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter, ThrowingEndpointFilter>()));

    private sealed class ThrowingEndpointFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == "/test/kasta")
                {
                    throw new InvalidOperationException(
                        "HEMLIG-INTERN-DETALJ-SOM-INTE-FAR-LACKA");
                }

                await nextMiddleware(context);
            });
        };
    }
}
