using KarraMatcher.Api.Caching;
using KarraMatcher.Api.Diagnostics;
using KarraMatcher.Api.Features.Auth;
using KarraMatcher.Application;
using KarraMatcher.Infrastructure;
using KarraMatcher.Infrastructure.Persistence;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog läses från konfiguration så att nivåer kan ändras per miljö utan omdeploy.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddKarraHealthChecks();
builder.Services.AddKarraRateLimiting(builder.Configuration);
builder.Services.AddKarraEdgeCache(builder.Configuration);
builder.Services.AddKarraAuthentication(builder.Environment);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    /*
     * Enum-varden som text, inte som siffror.
     *
     * En siffra tvingar klienten att kanna till enumens ordning, och att lagga till ett
     * varde i mitten andrar da tyst betydelsen av alla svar som redan skickats. Texten
     * gar dessutom att lasa i ett felmeddelande utan att sla upp nagot.
     */
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
});

builder.Services.AddProblemDetails();

// Ordningen är betydelsefull: valideringshanteraren måste få se felet först, annars
// blir en användares stavfel ett 500 i stället för ett 400.
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Migrationer och startdata körs bara när konfigurationen ber om det.
await app.Services.InitializeDatabaseAsync();

// Forwarded headers först av allt: allt nedanför behöver klientens riktiga IP,
// inte Vercels edge-adress (§KM.11).
app.UseForwardedHeaders();

// Correlation-ID därefter, så att även felhanterarens egna loggrader får med det.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

// Loggar en rad per request i stället för flera — kompakt och lätt att följa.
app.UseSerilogRequestLogging();

// Cache-headers före rate limitern, så att även ett 429-svar får no-store i stället
// för att bli liggande på edge (§KM.11).
app.UseKarraEdgeCache();

app.UseRateLimiter();

// Autentisering fore auktorisering -- annars ar anvandaren inte kand nar beslutet fattas.
// Bada ligger efter rate limitern, sa att ett flodesangrepp stoppas innan en signatur
// hinner verifieras.
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapKarraHealthChecks();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new { service = "KarraMatcher.Api", status = "up" }));

app.Run();

/// <summary>Exponeras för integrationstester (WebApplicationFactory).</summary>
public partial class Program;
