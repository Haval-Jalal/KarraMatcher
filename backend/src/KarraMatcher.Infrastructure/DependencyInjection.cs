using KarraMatcher.Application.Abstractions.Audit;
using KarraMatcher.Application.Abstractions.Email;
using KarraMatcher.Application.Abstractions.Geocoding;
using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Infrastructure.Email;
using KarraMatcher.Infrastructure.Geocoding;
using KarraMatcher.Infrastructure.Persistence;
using KarraMatcher.Infrastructure.Persistence.Repositories;
using KarraMatcher.Infrastructure.Persistence.Seed;
using KarraMatcher.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Infrastructure;

/// <summary>Registrerar infrastrukturlagrets tjänster.</summary>
public static class DependencyInjection
{
    public const string ConnectionStringName = "Default";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Anslutningssträngen kommer alltid från konfiguration — aldrig från kod.
        // Lokalt via user-secrets eller .env, i drift som miljövariabel i Render.
        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Anslutningssträngen '{ConnectionStringName}' saknas. Sätt "
                + $"ConnectionStrings__{ConnectionStringName} som miljövariabel, "
                + "eller kör 'dotnet user-secrets' lokalt. Se backend/.env.example.");
        }

        services.AddDbContext<KarraMatcherDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                // Neon stänger av beräkningen vid inaktivitet och startar den igen vid
                // nästa anslutning. Ett par försök gör den återstarten osynlig.
                npgsql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(5), null);
                npgsql.MigrationsAssembly(typeof(DependencyInjection).Assembly.FullName);
            }));

        /*
         * Inloggningens installningar valideras vid start, inte vid forsta inloggningen.
         * En saknad eller for kort signeringsnyckel ska falla driftsattningen medan nagon
         * tittar -- inte en lordagsmorgon nar en foralder forsoker logga in.
         */
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<IMatchRepository, MatchRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<ILoginCodeRepository, LoginCodeRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IMatchAdminRepository, MatchAdminRepository>();
        services.AddScoped<IVenueRepository, VenueRepository>();
        services.AddScoped<IScheduleImportRepository, ScheduleImportRepository>();
        services.AddScoped<ICarpoolOfferRepository, CarpoolOfferRepository>();
        services.AddScoped<ICarpoolRequestRepository, CarpoolRequestRepository>();

        /*
         * Adressuppslagning mot Nominatim (OpenStreetMap).
         *
         * Villkoren kraver att anroparen gar att identifiera -- att skicka anonymt vore att
         * bryta mot villkoren for en tjanst som drivs av frivilliga. Anropen ar fa: en gang
         * per spelplats, nar den sparas, och aldrig vid lasning.
         */
        services.AddHttpClient<IGeocoder, NominatimGeocoder>(client =>
        {
            client.BaseAddress = new Uri("https://nominatim.openstreetmap.org/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "KarraMatcher/1.0 (+https://github.com/Haval-Jalal/KarraMatcher)");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        AddEmail(services, configuration, isDevelopment);

        // Databaskontrollen taggas "ready". Därmed faller /health/ready när databasen
        // är onåbar, medan /health fortsätter svara — se §KM.11 och issue #8.
        services.AddHealthChecks()
            .AddDbContextCheck<KarraMatcherDbContext>("database", tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Väljer mejlleverantör efter miljö och konfiguration.
    ///
    /// <para>
    /// <b>I drift utan nyckel faller uppstarten.</b> Att i stället tyst låta bli att
    /// skicka hade varit det sämsta utfallet: allt ser ut att fungera, ingen kommer in,
    /// och felet upptäcks först när en förälder hör av sig. Ett fel vid start upptäcks
    /// medan någon fortfarande tittar på driftsättningen.
    /// </para>
    ///
    /// <para>
    /// I utveckling utan nyckel skrivs mejlet i konsolen i stället, eftersom koden annars
    /// inte går att få tag på — den lagras hashad och finns bara i mejlet.
    /// </para>
    /// </summary>
    private static void AddEmail(
        IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment)
    {
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        var apiKey = configuration[$"{EmailOptions.SectionName}:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (!isDevelopment)
            {
                throw new InvalidOperationException(
                    $"{EmailOptions.SectionName}__ApiKey saknas. Utan den kan inga "
                    + "inloggningskoder skickas. Sätt den som miljövariabel i Render, "
                    + "eller kör 'dotnet user-secrets' lokalt. Se backend/.env.example.");
            }

            services.AddScoped<IEmailSender, DevelopmentEmailSender>();

            return;
        }

        services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            // Kort tak: en inloggning far inte hanga pa att leverantoren ar seg.
            client.Timeout = TimeSpan.FromSeconds(10);
        });
    }
}
