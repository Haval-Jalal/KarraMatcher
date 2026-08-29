using KarraMatcher.Application;
using KarraMatcher.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Tillfällig livstecken-endpoint. Riktiga health checks, ProblemDetails,
// Serilog och correlation-ID kommer i issue #8.
app.MapGet("/", () => Results.Ok(new { service = "KarraMatcher.Api", status = "up" }));

app.Run();

/// <summary>Exponeras för integrationstester (WebApplicationFactory).</summary>
public partial class Program;
