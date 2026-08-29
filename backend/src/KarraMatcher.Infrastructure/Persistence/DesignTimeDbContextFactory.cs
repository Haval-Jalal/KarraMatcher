using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KarraMatcher.Infrastructure.Persistence;

/// <summary>
/// Används bara av <c>dotnet ef</c> vid designtid. Utan den skulle EF starta hela
/// API:t för att hitta contexten, och <c>AddInfrastructure</c> vägrar starta utan
/// anslutningssträng — vilket gjorde det omöjligt att ens skapa en migration.
///
/// Att <em>skapa</em> en migration kräver ingen databas. Att <em>applicera</em> den
/// gör det, och då måste <c>ConnectionStrings__Default</c> vara satt.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KarraMatcherDbContext>
{
    private const string DesignTimePlaceholder =
        "Host=localhost;Database=karramatcher;Username=postgres;Password=postgres";

    public KarraMatcherDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? DesignTimePlaceholder;

        var options = new DbContextOptionsBuilder<KarraMatcherDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new KarraMatcherDbContext(options);
    }
}
