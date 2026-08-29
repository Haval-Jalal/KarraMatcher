using System.Reflection;

using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Application;

/// <summary>Registrerar applikationslagrets tjänster.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<IQueryDispatcher, QueryDispatcher>();

        // Öppen generisk registrering: behavioren gäller varje fråga utan att någon
        // behöver komma ihåg att koppla in den.
        services.AddScoped(typeof(IQueryBehavior<,>), typeof(ValidationBehavior<,>));

        AddQueryHandlers(services, assembly);

        return services;
    }

    /// <summary>
    /// Registrerar varje <see cref="IQueryHandler{TQuery, TResult}"/> i assemblyn.
    ///
    /// <para>
    /// Automatiskt och inte en rad per handler, av samma skäl som validatorerna: en ny
    /// handler ska fungera för att den finns, inte för att någon kom ihåg att registrera
    /// den. Ett arkitekturtest kontrollerar att uppslagningen faktiskt hittar dem.
    /// </para>
    /// </summary>
    private static void AddQueryHandlers(IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            var handlerInterfaces = type.GetInterfaces().Where(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>));

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddScoped(handlerInterface, type);
            }
        }
    }
}
