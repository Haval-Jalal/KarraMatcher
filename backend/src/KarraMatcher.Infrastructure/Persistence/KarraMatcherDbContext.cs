using System.Reflection;

using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;

using Microsoft.EntityFrameworkCore;

namespace KarraMatcher.Infrastructure.Persistence;

public sealed class KarraMatcherDbContext(DbContextOptions<KarraMatcherDbContext> options)
    : DbContext(options)
{
    public DbSet<Club> Clubs => Set<Club>();

    public DbSet<AgeGroup> AgeGroups => Set<AgeGroup>();

    public DbSet<Team> Teams => Set<Team>();

    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<Match> Matches => Set<Match>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}
