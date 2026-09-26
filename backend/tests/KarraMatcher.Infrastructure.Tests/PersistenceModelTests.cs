using KarraMatcher.Domain.Events;
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
    [InlineData(nameof(Event.KickoffUtc))]
    [InlineData(nameof(Event.UpdatedUtc))]
    public void Tidsstamplar_LagrasSomTimestamptz(string propertyName)
    {
        // timestamptz, inte timestamp. Npgsql vägrar då skriva en DateTime vars Kind
        // inte är Utc — vilket är körtidsskyddet bakom §KM.5.
        Assert.Equal(
            "timestamp with time zone",
            Property<Event>(propertyName).GetColumnType());
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
    public void EventStatus_LagrasSomTextInteSiffra()
    {
        // En siffra i databasen säger ingenting den dag någon felsöker med psql.
        var property = Property<Event>(nameof(Event.Status));

        Assert.Equal("character varying(20)", property.GetColumnType());
        Assert.Equal(EventStatus.Scheduled, property.GetDefaultValue());
    }

    [Fact]
    public void Matcher_HarIndexPaLagOchAvspark()
    {
        // Appens vanligaste fråga: ett lags matcher i tidsordning.
        var entity = Model().FindEntityType(typeof(Event));
        Assert.NotNull(entity);

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name)
                .SequenceEqual([nameof(Event.TeamId), nameof(Event.KickoffUtc)]));

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
        var entity = Model().FindEntityType(typeof(Event));
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
                "Account", "AgeGroup", "AttendanceCall", "AttendanceInvitation", "AuditEntry",
                "CarpoolOffer", "CarpoolRequest", "ChatMessage", "ChatReaction", "ChatReport", "Child", "Club", "Event",
                "GuardianConsent",
                "Guardianship", "Invitation",
                "LoginCode", "MembershipApplication", "NotificationPreference",
                "PushSubscription", "RefreshToken", "Sport",
                "Team", "TeamRole", "Venue",
            ],
            names.OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void Kallelseinbjudan_ForsvinnerMedBarnet()
    {
        // §KM.6/§KM.1: nar ett barn tas bort ur truppen ska dess kallelse-rader folja med.
        // Kallelsesvaret bar inte nagon konto-nyckel (RespondedByAccountId har ingen FK, som
        // audit-raden) -- svaret overlever att den svarandes konto raderas.
        var entity = Model().FindEntityType(typeof(Domain.Attendance.AttendanceInvitation))!;

        var toChild = entity.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Domain.Children.Child));

        Assert.Equal(DeleteBehavior.Cascade, toChild.DeleteBehavior);
    }

    [Fact]
    public void Kallelse_HarUniktIndexPerMatch()
    {
        // En match kallas en gang. Oppnandet ar idempotent i handlern; indexet ar garantin
        // mot att tva samtidiga anrop skapar tva kallelser. Kontrolleras mot Npgsql-modellen,
        // eftersom InMemory struntar i unika index.
        var entity = Model().FindEntityType(typeof(Domain.Attendance.AttendanceCall));
        Assert.NotNull(entity);

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Count == 1
            && i.Properties[0].Name == nameof(Domain.Attendance.AttendanceCall.MatchId));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Kallelseinbjudan_HarUniktIndexPerBarnOchKallelse()
    {
        // Ett barn kallas en gang per kallelse. Indexet ar garantin mot dubbletter och
        // tjanar summeringen per kallelse via sitt CallId-prefix.
        var entity = Model().FindEntityType(typeof(Domain.Attendance.AttendanceInvitation));
        Assert.NotNull(entity);

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(
                [
                    nameof(Domain.Attendance.AttendanceInvitation.CallId),
                    nameof(Domain.Attendance.AttendanceInvitation.ChildId),
                ]));

        Assert.NotNull(index);
        Assert.True(index.IsUnique);
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
    public void Pushprenumeration_TapparKontokopplingenNarKontotRaderas()
    {
        // §KM.6, beslut 2026-09-14: SetNull, inte Cascade. En prenumeration hor till enheten,
        // inte till kontot -- raderas kontot slapper bara kopplingen, och enheten fortsatter
        // fa lagets matchnotiser. Cascade hade tyst tagit bort en notis enheten sjalv bett om.
        var entity = Model().FindEntityType(typeof(Domain.Push.PushSubscription))!;

        var toAccount = entity.GetForeignKeys()
            .Single(fk => fk.PrincipalEntityType.ClrType == typeof(Domain.Accounts.Account));

        Assert.Equal(DeleteBehavior.SetNull, toAccount.DeleteBehavior);
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

    /// <summary>
    /// En aktiv förfrågan per person och erbjudande — som ett filtrerat unikt index.
    ///
    /// <para>
    /// <b>Kontrolleras här och inte i integrationstesterna med flit.</b> De kör mot EF:s
    /// InMemory-provider, som varken bryr sig om unika index eller filter — ett test där
    /// hade gått grönt vad än indexet sa, vilket är värre än inget test alls. Modellen
    /// byggs däremot mot Npgsql, så det som står här är det som hamnar i databasen.
    /// </para>
    ///
    /// <para>
    /// Kontrollen i handlern ger det begripliga felet; indexet ger garantin. Två anrop som
    /// kommer samtidigt hinner båda läsa "ingen aktiv finns" innan någon av dem skrivit.
    /// </para>
    /// </summary>
    [Fact]
    public void Akforfragan_HarUniktIndexForAktivaPerPersonOchErbjudande()
    {
        var entity = Model().FindEntityType(typeof(Domain.Carpool.CarpoolRequest));
        Assert.NotNull(entity);

        var index = entity.GetIndexes().SingleOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(
                [
                    nameof(Domain.Carpool.CarpoolRequest.OfferId),
                    nameof(Domain.Carpool.CarpoolRequest.RequesterAccountId),
                ]));

        Assert.NotNull(index);
        Assert.True(index.IsUnique, "Indexet maste vara unikt, annars garanterar det ingenting.");

        var filter = index.GetFilter();

        Assert.NotNull(filter);

        // Filtret ar hela poangen: en nekad eller atertagen forfragan far inte blockera en
        // ny, och utan filter hade indexet last ute den som fragat en gang.
        Assert.Contains("Pending", filter, StringComparison.Ordinal);
        Assert.Contains("Accepted", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("Retracted", filter, StringComparison.Ordinal);
        Assert.DoesNotContain("Denied", filter, StringComparison.Ordinal);
    }
}
