using KarraMatcher.Api.Diagnostics;
using KarraMatcher.Application;
using KarraMatcher.Infrastructure;

using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog läses från konfiguration så att nivåer kan ändras per miljö utan omdeploy.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddKarraHealthChecks();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Correlation-ID först, så att även felhanterarens egna loggrader får med det.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();

// Loggar en rad per request i stället för flera — kompakt och lätt att följa.
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapKarraHealthChecks();

app.MapGet("/", () => Results.Ok(new { service = "KarraMatcher.Api", status = "up" }));

app.Run();

/// <summary>Exponeras för integrationstester (WebApplicationFactory).</summary>
public partial class Program;
