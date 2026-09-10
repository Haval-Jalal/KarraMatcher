using System.Collections.Concurrent;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Push;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Utskicksjobbet (`#61`).
///
/// <para>
/// Tre saker prövas, och alla tre är sådant som annars märks först i drift: att utskicket
/// sker utanför requesten, att en död prenumeration städas bort i stället för att försökas
/// i all evighet, och att en enskild mottagare som strular inte fäller utskicket till
/// resten av laget.
/// </para>
///
/// <para>
/// Sändaren är utbytt mot en attrapp. Att skjuta riktiga notiser mot Google i ett test hade
/// varit långsamt, opålitligt och beroende av nycklar som inte finns i CI — kryptot prövas
/// för sig i <c>WebPushEncryptionTests</c>.
/// </para>
/// </summary>
public sealed class PushDispatchTests
{
    /// <summary>Vad attrappen ska svara för en viss adress.</summary>
    private sealed class FakeSender : IPushSender
    {
        public ConcurrentDictionary<string, PushOutcome> Outcomes { get; } = new(StringComparer.Ordinal);

        public ConcurrentDictionary<string, int> Attempts { get; } = new(StringComparer.Ordinal);

        /// <summary>Adresser som ska svara Retry första gången och lyckas sedan.</summary>
        public ConcurrentDictionary<string, int> RetriesBeforeSuccess { get; } = new(StringComparer.Ordinal);

        public Task<PushOutcome> SendAsync(
            PushTarget target,
            PushMessage message,
            CancellationToken cancellationToken)
        {
            var attempt = Attempts.AddOrUpdate(target.Endpoint, 1, (_, value) => value + 1);

            if (RetriesBeforeSuccess.TryGetValue(target.Endpoint, out var needed) && attempt <= needed)
            {
                return Task.FromResult(PushOutcome.Retry);
            }

            return Task.FromResult(
                Outcomes.TryGetValue(target.Endpoint, out var outcome) ? outcome : PushOutcome.Delivered);
        }
    }

    private static WebApplicationFactory<Program> WithFakeSender(
        KarraMatcherApiFactory factory,
        FakeSender sender) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushSender>();
            services.AddSingleton<IPushSender>(sender);
        }));

    private static async Task<(Guid TeamId, string[] Endpoints)> SeedAsync(
        WebApplicationFactory<Program> factory,
        string suffix,
        int subscribers)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-d-{suffix}" };
        var ageGroup = new AgeGroup
        {
            Id = Guid.NewGuid(),
            ClubId = club.Id,
            Name = "P2016",
            Season = "2026",
        };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = ageGroup.Id,
            Name = "Gul",
            ColorHex = "#D9A21B",
            Slug = $"gul-d-{suffix}",
        };

        var endpoints = Enumerable.Range(0, subscribers)
            .Select(index => $"https://push.example/{suffix}/{index}")
            .ToArray();

        context.Clubs.Add(club);
        context.AgeGroups.Add(ageGroup);
        context.Teams.Add(team);

        foreach (var endpoint in endpoints)
        {
            context.PushSubscriptions.Add(new PushSubscription
            {
                Id = Guid.NewGuid(),
                TeamId = team.Id,
                Endpoint = endpoint,
                P256dh = "nyckel",
                Auth = "hemlighet",
                CreatedUtc = DateTime.UtcNow,
            });
        }

        await context.SaveChangesAsync(CancellationToken.None);

        return (team.Id, endpoints);
    }

    /// <summary>
    /// Väntar in bakgrundstjänsten.
    ///
    /// <para>
    /// Ett utskick är med flit inte synkront — det är hela poängen med jobbet — så testet
    /// måste fråga tills det hänt. Tidsgränsen är generös nog att inte vara flaky och kort
    /// nog att ett verkligt fel märks som ett fel och inte som en hängning.
    /// </para>
    /// </summary>
    private static async Task<bool> EventuallyAsync(Func<Task<bool>> condition, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);

        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(50, CancellationToken.None);
        }

        return false;
    }

    private static async Task<int> CountAsync(WebApplicationFactory<Program> factory, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        return await context.PushSubscriptions
            .AsNoTracking()
            .CountAsync(s => s.TeamId == teamId, CancellationToken.None);
    }

    [Fact]
    public async Task Utskicket_NarAllaLagetsPrenumeranter()
    {
        using var factory = new KarraMatcherApiFactory();
        var sender = new FakeSender();
        using var host = WithFakeSender(factory, sender);

        var (teamId, endpoints) = await SeedAsync(host, "alla", subscribers: 3);

        // Klienten startar varden, sa bakgrundstjansten kor.
        using var client = host.CreateClient();

        host.Services.GetRequiredService<IPushOutbox>()
            .Enqueue(new PushDispatch(teamId, new PushMessage("Matchen ar installd", "", "/match/1")));

        Assert.True(await EventuallyAsync(() =>
            Task.FromResult(endpoints.All(sender.Attempts.ContainsKey))));
    }

    [Fact]
    public async Task DodPrenumeration_TasBort_OchResten_FarSinNotis()
    {
        /*
         * 404 och 410 betyder att webblasaren ar borta for gott. Att lata raden ligga kvar
         * ar bade ett bortkastat anrop vid varje utskick och en personuppgift utan andamal
         * (§KM.10) -- och den far absolut inte falla utskicket till resten av laget.
         */
        using var factory = new KarraMatcherApiFactory();
        var sender = new FakeSender();
        using var host = WithFakeSender(factory, sender);

        var (teamId, endpoints) = await SeedAsync(host, "dod", subscribers: 3);

        sender.Outcomes[endpoints[1]] = PushOutcome.Gone;

        using var client = host.CreateClient();

        host.Services.GetRequiredService<IPushOutbox>()
            .Enqueue(new PushDispatch(teamId, new PushMessage("Ny tid", "", "/match/1")));

        Assert.True(await EventuallyAsync(async () => await CountAsync(host, teamId) == 2));

        // De andra tva fick sin notis anda.
        Assert.True(sender.Attempts.ContainsKey(endpoints[0]));
        Assert.True(sender.Attempts.ContainsKey(endpoints[2]));
    }

    [Fact]
    public async Task TillfalligtFel_ForsokerIgen()
    {
        // 5xx och natverksfel ar tillfalliga. Ett enda forsok hade gjort en notis beroende
        // av att Google svarar just den sekunden.
        using var factory = new KarraMatcherApiFactory();
        var sender = new FakeSender();
        using var host = WithFakeSender(factory, sender);

        var (teamId, endpoints) = await SeedAsync(host, "retry", subscribers: 1);

        sender.RetriesBeforeSuccess[endpoints[0]] = 1;

        using var client = host.CreateClient();

        host.Services.GetRequiredService<IPushOutbox>()
            .Enqueue(new PushDispatch(teamId, new PushMessage("Flyttad match", "", "/match/1")));

        Assert.True(await EventuallyAsync(() =>
            Task.FromResult(sender.Attempts.TryGetValue(endpoints[0], out var attempts) && attempts >= 2)));

        // Prenumerationen lever -- ett tillfalligt fel far inte tolkas som en dod enhet.
        Assert.Equal(1, await CountAsync(host, teamId));
    }

    [Fact]
    public async Task LyckatUtskick_NoterasPaPrenumerationen()
    {
        // Gallringen i en senare version behover veta vad som lever. Utan det matt gar det
        // inte att skilja en tyst enhet fran en dod.
        using var factory = new KarraMatcherApiFactory();
        var sender = new FakeSender();
        using var host = WithFakeSender(factory, sender);

        var (teamId, _) = await SeedAsync(host, "noterat", subscribers: 1);

        using var client = host.CreateClient();

        host.Services.GetRequiredService<IPushOutbox>()
            .Enqueue(new PushDispatch(teamId, new PushMessage("Ny match", "", "/match/1")));

        Assert.True(await EventuallyAsync(async () =>
        {
            using var scope = host.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

            return await context.PushSubscriptions
                .AsNoTracking()
                .AnyAsync(s => s.TeamId == teamId && s.LastUsedUtc != null, CancellationToken.None);
        }));
    }

    [Fact]
    public async Task LagUtanPrenumeranter_GerIngenSandning()
    {
        // Vanligast av allt innan nagon hunnit tillata notiser. Det ska vara en tyst
        // ickehandelse, inte ett fel i loggen vid varje matchandring.
        using var factory = new KarraMatcherApiFactory();
        var sender = new FakeSender();
        using var host = WithFakeSender(factory, sender);

        var (teamId, _) = await SeedAsync(host, "tomt", subscribers: 0);

        using var client = host.CreateClient();

        host.Services.GetRequiredService<IPushOutbox>()
            .Enqueue(new PushDispatch(teamId, new PushMessage("Ny match", "", "/match/1")));

        await Task.Delay(300, CancellationToken.None);

        Assert.Empty(sender.Attempts);
    }
}
