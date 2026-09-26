using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Notis-payloaden bär aldrig ett barns namn eller en förälders fritext (§KM.1/§KM.2/§KM.10).
///
/// <para>
/// En låsskärm ligger öppen i rummet, och en notis stannar i notiscentret. Därför får en push
/// bara säga <em>att</em> något hänt och ta den som vill veta mer in i appen — aldrig "Liam är
/// kallad" eller en inklistrad chatt-rad. Byggarna är neutrala i dag; det här kör flödena med
/// distinkt känslig data och fäller bygget om den någonsin dyker upp i det som köas.
/// </para>
/// </summary>
public sealed class NotificationPiiTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private const string ChildFirstName = "LiamHemligtNamn";
    private const string ChatFreeText = "HemligChattTextXYZ";

    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private sealed record Fixture(Guid TruppId, Guid EventId, Guid GuardianId, Guid ChildId);

    private (WebApplicationFactory<Program> App, RecordingOutbox Outbox) WithRecordingOutbox()
    {
        var outbox = new RecordingOutbox();

        var app = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPushOutbox>();
                services.AddSingleton<IPushOutbox>(outbox);
            }));

        return (app, outbox);
    }

    [Fact]
    public async Task Notiser_BarAldrigBarnnamnEllerFritext()
    {
        var f = await SeedAsync("pii");
        var (app, outbox) = WithRecordingOutbox();

        // Kallelse för barnet — barnets namn är den känsliga datan. Push:en ska säga "Ditt barn
        // är kallat", aldrig namnet.
        await SendAsync(
            app,
            HttpMethod.Put,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.EventId}/kallelse",
            AdminToken(f.TruppId),
            new { childIds = new[] { f.ChildId } });

        // Chatt-meddelande — fritext från en förälder. Push:en ska säga "Nytt meddelande i
        // chatten", aldrig texten.
        await SendAsync(
            app,
            HttpMethod.Post,
            $"/api/v1/trupper/{f.TruppId}/chat/messages",
            PlainToken(f.GuardianId),
            new { body = ChatFreeText });

        // Något måste ha köats, annars vaktar testet ingenting.
        Assert.NotEmpty(outbox.Dispatches);

        foreach (var dispatch in outbox.Dispatches)
        {
            var payload = string.Join(
                '\n',
                dispatch.Message.Title,
                dispatch.Message.Body,
                dispatch.Message.Url);

            Assert.DoesNotContain(ChildFirstName, payload, StringComparison.Ordinal);
            Assert.DoesNotContain(ChatFreeText, payload, StringComparison.Ordinal);
        }
    }

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-p-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-p-{suffix}",
            AttendanceEnabled = true,
        };
        var match = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            Type = EventType.Match,
            KickoffUtc = now.AddDays(3),
            OpponentName = "Torslanda",
            IsHome = true,
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Events.Add(match);
        await context.SaveChangesAsync(CancellationToken.None);

        // Två vårdnadshavare med varsitt barn i laget: chatt-push:en behöver en mottagare utöver
        // avsändaren.
        var (child, guardian) = await SeedChildAsync(suffix, "g1", trupp.Id, team.Id);
        await SeedChildAsync(suffix, "g2", trupp.Id, team.Id);

        return new Fixture(trupp.Id, match.Id, guardian, child);
    }

    private async Task<(Guid ChildId, Guid GuardianId)> SeedChildAsync(
        string suffix, string tag, Guid truppId, Guid teamId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var guardian = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"vh-{tag}-p-{suffix}@example.com",
            CreatedUtc = now,
        };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = ChildFirstName,
            LastInitial = "J",
            AgeGroupId = truppId,
            TeamId = teamId,
            CreatedUtc = now,
        };

        context.Accounts.Add(guardian);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });
        await context.SaveChangesAsync(CancellationToken.None);

        return (child.Id, guardian.Id);
    }

    private string AdminToken(Guid truppId) =>
        TestAuth.TokenFor(
            factory.Services,
            Guid.NewGuid(),
            new AccountRoles(false, [truppId.ToString()], []),
            "admin@example.com");

    private string PlainToken(Guid accountId) =>
        TestAuth.TokenFor(factory.Services, accountId, AccountRoles.None, "konto@example.com");

    private static async Task SendAsync(
        WebApplicationFactory<Program> app,
        HttpMethod method,
        string path,
        string token,
        object payload)
    {
        using var client = app.CreateClient(ClientOptions);
        var (csrf, cookie) = await CsrfAsync(client, token);

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", csrf);
        request.Headers.Add("Cookie", cookie);
        request.Content = JsonContent.Create(payload);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<(string Token, string Cookie)> CsrfAsync(HttpClient client, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        return (body.GetProperty("token").GetString()!, cookie);
    }
}
