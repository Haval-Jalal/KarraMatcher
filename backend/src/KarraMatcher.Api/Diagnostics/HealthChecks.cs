using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KarraMatcher.Api.Diagnostics;

/// <summary>
/// Två skilda kontroller:
/// <list type="bullet">
///   <item><c>/health</c> — lever processen? Rör inga beroenden. Render och
///   uppetidsverktyget frågar den här, ofta.</item>
///   <item><c>/health/ready</c> — kan tjänsten ta emot trafik? Här samlas beroenden,
///   taggade <c>ready</c>. Databaskontrollen läggs till i #6.</item>
/// </list>
/// </summary>
public static class HealthChecks
{
    public const string ReadyTag = "ready";

    public static IServiceCollection AddKarraHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("Processen svarar"));

        return services;
    }

    public static void MapKarraHealthChecks(this WebApplication app)
    {
        // Liveness: undantar allt taggat "ready" så att en trasig databas inte får
        // Render att döda en container som i övrigt fungerar.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = registration => !registration.Tags.Contains(ReadyTag),
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
        });
    }
}
