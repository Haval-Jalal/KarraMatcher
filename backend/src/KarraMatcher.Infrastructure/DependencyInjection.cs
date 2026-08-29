using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Infrastructure;

/// <summary>
/// Registrerar infrastrukturlagrets tjänster. Tomt tills databasen kopplas in
/// i issue #6 — YAGNI enligt CLAUDE.md §0 p.5.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }
}
