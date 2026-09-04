using System.Reflection;

using FluentValidation;

using KarraMatcher.Application.Abstractions.Messaging;
using KarraMatcher.Application.Features.Auth;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        // Öppen generisk registrering: behavioren gäller varje fråga utan att någon
        // behöver komma ihåg att koppla in den.
        services.AddScoped(typeof(IQueryBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(ICommandBehavior<,>), typeof(CommandValidationBehavior<,>));

        AddQueryHandlers(services, assembly);
        AddCommandHandlers(services, assembly);

        // Klockan injiceras för att sessionernas livstider ska gå att pröva. Ett test som
        // väntar in att en token går ut hade tagit 60 dagar.
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<SessionIssuer>();
        services.AddScoped<LoginCodeService>();
        services.AddScoped<Features.Matches.Admin.MatchAdminService>();
        services.AddScoped<Features.Venues.VenueRegistry>();
        services.AddScoped<Features.Matches.Import.ScheduleImportService>();
        services.AddScoped<Features.Carpool.CarpoolOfferService>();
        services.AddScoped<Features.Carpool.CarpoolRequestService>();

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
        Register(services, assembly, typeof(IQueryHandler<,>));
    }

    /// <summary>Samma sak för kommandon — se <see cref="AddQueryHandlers"/>.</summary>
    private static void AddCommandHandlers(IServiceCollection services, Assembly assembly)
    {
        Register(services, assembly, typeof(ICommandHandler<,>));
    }

    private static void Register(IServiceCollection services, Assembly assembly, Type openInterface)
    {
        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            var handlerInterfaces = type.GetInterfaces().Where(i =>
                i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);

            foreach (var handlerInterface in handlerInterfaces)
            {
                services.AddScoped(handlerInterface, type);
            }
        }
    }
}
