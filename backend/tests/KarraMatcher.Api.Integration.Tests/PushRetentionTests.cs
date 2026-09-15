using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Push;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Gallringen av tysta push-prenumerationer (`#68`, säkerhetschecklistan 9.8).
///
/// <para>
/// En teknisk adress till en webbläsare är en personuppgift (§KM.10). Den döda
/// prenumerationen städas reaktivt när ett utskick får 404/410; den här sveper upp de som
/// aldrig får ett utskick och därför aldrig hör av sig — en telefon som lagts undan. Två
/// gränsfall vaktas särskilt: att en tyst prenumeration verkligen försvinner, och att en
/// aldrig-använd bedöms på när den skapades, inte på ett null-fält.
/// </para>
/// </summary>
public sealed class PushRetentionTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    /// <summary>Skapar en prenumeration med given senaste användning och skapelsetid.</summary>
    private async Task<Guid> SeedAsync(DateTime createdUtc, DateTime? lastUsedUtc)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var subscription = new PushSubscription
        {
            Id = Guid.NewGuid(),
            TeamId = Guid.NewGuid(),
            AccountId = null,
            Endpoint = $"https://push.example.com/{Guid.NewGuid():N}",
            P256dh = "nyckel",
            Auth = "auth",
            CreatedUtc = createdUtc,
            LastUsedUtc = lastUsedUtc,
        };

        context.PushSubscriptions.Add(subscription);
        await context.SaveChangesAsync(CancellationToken.None);

        return subscription.Id;
    }

    private async Task<int> PurgeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var retention = scope.ServiceProvider.GetRequiredService<PushRetentionService>();

        return await retention.PurgeAsync(CancellationToken.None);
    }

    private async Task<bool> ExistsAsync(Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.PushSubscriptions
            .AsNoTracking()
            .AnyAsync(s => s.Id == id, CancellationToken.None);
    }

    [Fact]
    public async Task Gallring_TarBortPrenumerationTystSedanForeGransen()
    {
        var now = DateTime.UtcNow;
        var silent = await SeedAsync(now.AddDays(-400), now.AddDays(-366));

        await PurgeAsync();

        Assert.False(await ExistsAsync(silent));
    }

    [Fact]
    public async Task Gallring_LamnarKvarPrenumerationAnvandNyligen()
    {
        // Använd för en dryg månad sedan men långt innanför tolv månader — fortfarande en
        // telefon som lyssnar.
        var now = DateTime.UtcNow;
        var active = await SeedAsync(now.AddDays(-400), now.AddDays(-364));

        await PurgeAsync();

        Assert.True(await ExistsAsync(active));
    }

    [Fact]
    public async Task Gallring_TarAldrigAnvandEfterSkapelsetiden()
    {
        // Gränsfallet: LastUsedUtc sätts först vid en lyckad leverans. En prenumeration som
        // aldrig fått ett utskick har det som null och måste bedömas på CreatedUtc — annars
        // gallras den aldrig.
        var now = DateTime.UtcNow;
        var neverUsedOld = await SeedAsync(now.AddDays(-366), lastUsedUtc: null);

        await PurgeAsync();

        Assert.False(await ExistsAsync(neverUsedOld));
    }

    [Fact]
    public async Task Gallring_LamnarKvarNyskapadAldrigAnvand()
    {
        // En nyss skapad prenumeration som ännu inte fått något utskick ska inte tas för död.
        var now = DateTime.UtcNow;
        var neverUsedFresh = await SeedAsync(now.AddDays(-1), lastUsedUtc: null);

        await PurgeAsync();

        Assert.True(await ExistsAsync(neverUsedFresh));
    }

    [Fact]
    public async Task Gallring_RaknarVadSomTogsBort()
    {
        var now = DateTime.UtcNow;
        await SeedAsync(now.AddDays(-400), now.AddDays(-366));

        var removed = await PurgeAsync();

        Assert.True(removed >= 1);
    }

    [Fact]
    public async Task Gallring_GarAttKoraTvaGanger()
    {
        // Jobbet kör vid varje uppstart, och Render free vaknar flera gånger om dagen. Ett
        // andra varv får inte kasta — det ska bara inte hitta något.
        var now = DateTime.UtcNow;
        await SeedAsync(now.AddDays(-400), now.AddDays(-366));

        await PurgeAsync();
        var second = await PurgeAsync();

        Assert.Equal(0, second);
    }
}
