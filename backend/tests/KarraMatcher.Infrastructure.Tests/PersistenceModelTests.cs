using KarraMatcher.Domain.Matches;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace KarraMatcher.Infrastructure.Tests;

/// <summary>
/// Låser fast de mappningar som är medvetna beslut, så att de inte glider bort.
/// Modellen byggs mot Npgsql — utan att någon databas behöver finnas.
/// </summary>
public class PersistenceModelTests
{
    private static IModel Model()
    {
        var options = new DbContextOptionsBuilder<KarraMatcherDbContext>()
            .UseNpgsql("Host=modell;Database=modell;Username=x;Password=y")
            .Options;

        using var context = new KarraMatcherDbContext(options);
        return context.Model;
    }

    private static IProperty Property<TEntity>(string name)
    {
        var entity = Model().FindEntityType(typeof(TEntity));
        Assert.NotNull(entity);

        var property = entity.FindProperty(name);
        Assert.NotNull(property);
        return property;
    }

    [Theory]
    [InlineData(nameof(Match.KickoffUtc))]
    [InlineData(nameof(Match.UpdatedUtc))]
    public void Tidsstamplar_LagrasSomTimestamptz(string propertyName)
    {
        // timestamptz, inte timestamp. Npgsql vägrar då skriva en DateTime vars Kind
        // inte är Utc — vilket är körtidsskyddet bakom §KM.5.
        Assert.Equal(
            "timestamp with time zone",
            Property<Match>(propertyName).GetColumnType());
    }

    [Fact]
    public void AttendanceEnabled_HarStandardvardeFalseIDatabasen()
    {
        // Kallelsen levereras avstängd (§KM.7). Standardvärdet ligger i databasen så
        // att en rad som skapas utanför appen inte råkar slå på funktionen.
        var property = Property<Team>(nameof(Team.AttendanceEnabled));

        Assert.Equal(false, property.GetDefaultValue());
        Assert.False(property.IsNullable);
    }

    [Fact]
    public void MatchStatus_LagrasSomTextInteSiffra()
    {
        // En siffra i databasen säger ingenting den dag någon felsöker med psql.
        var property = Property<Match>(nameof(Match.Status));

        Assert.Equal("character varying(20)", property.GetColumnType());
        Assert.Equal(MatchStatus.Scheduled, property.GetDefaultValue());
    }

    [Fact]
    public void Matcher_HarIndexPaLagOchAvspark()
    {
        // Appens vanligaste fråga: ett lags matcher i tidsordning.
        var entity = Model().FindEntityType(typeof(Match));
        Assert.NotNull(entity);

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(Match.TeamId), nameof(Match.KickoffUtc)]));

        Assert.NotNull(index);
    }

    [Theory]
    [InlineData(typeof(Club), nameof(Club.Slug))]
    [InlineData(typeof(Team), nameof(Team.Slug))]
    public void Slugar_ArUnika(Type entityType, string propertyName)
    {
        // Slugen är en publik URL. Två lag med samma slug vore tyst datakorruption.
        var entity = Model().FindEntityType(entityType);
        Assert.NotNull(entity);

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0].Name == propertyName);

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Spelplats_KanInteRaderasNarMatcherAnvanderDen()
    {
        var entity = Model().FindEntityType(typeof(Match));
        Assert.NotNull(entity);

        var toVenue = entity.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Venue));

        Assert.Equal(DeleteBehavior.Restrict, toVenue.DeleteBehavior);
    }

    [Fact]
    public void AllaEntiteterFinnsIModellen()
    {
        // Namnet sa tidigare "AllaFem". Kontot och refresh-token tillkom i #30, och
        // raknandet i ett testnamn aldras samre an listan sjalv.
        var names = Model().GetEntityTypes().Select(e => e.ClrType.Name).ToHashSet();

        Assert.Equal(
            [
                "Account", "AgeGroup", "AuditEntry", "Club", "LoginCode", "Match",
                "RefreshToken", "Team", "TeamRole", "Venue",
            ],
            names.OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void RefreshToken_ForsvinnerMedSittKonto()
    {
        // Kontoradering ska ta med sig sessionerna (checklistan 1.6). Restrict eller
        // SetNull hade lamnat kvar tokens som pekar pa ett konto som inte finns.
        var entity = Model().FindEntityType(typeof(Domain.Accounts.RefreshToken))!;

        var toAccount = entity.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Domain.Accounts.Account));

        Assert.Equal(DeleteBehavior.Cascade, toAccount.DeleteBehavior);
    }

    [Fact]
    public void Sessionstider_LagrasMedTidszon()
    {
        // §KM.5: allt i UTC. En token som gar ut "lokal tid" ar en token som gar ut fel
        // timme tva ganger om aret.
        var entity = Model().FindEntityType(typeof(Domain.Accounts.RefreshToken))!;

        foreach (var property in new[] { "CreatedUtc", "ExpiresUtc", "ReplacedUtc", "RevokedUtc" })
        {
            Assert.Equal(
                "timestamp with time zone",
                entity.FindProperty(property)!.GetColumnType());
        }
    }
}
