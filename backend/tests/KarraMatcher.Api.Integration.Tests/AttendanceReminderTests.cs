using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Push;
using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Application.Features.Push;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Påminnelsen om kallelsen (§KM.7, §KM.1, `#199`).
///
/// <para>
/// Påminnelsen går till vårdnadshavarna för de kallade barn som ännu inte svarat — aldrig
/// till någon vars barn redan svarat, och bär ingen fritext eller något barns namn.
/// </para>
/// </summary>
public sealed class AttendanceReminderTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static WebApplicationFactoryClientOptions ClientOptions => new() { HandleCookies = true };

    private sealed class RecordingOutbox : IPushOutbox
    {
        public ConcurrentQueue<PushDispatch> Dispatches { get; } = new();

        public void Enqueue(PushDispatch dispatch) => Dispatches.Enqueue(dispatch);
    }

    private sealed record Fixture(Guid TruppId, Guid EventId);

    private (WebApplicationFactory<Program> App, RecordingOutbox Outbox) WithRecordingOutbox()
    {
        var outbox = new RecordingOutbox();
        var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPushOutbox>();
            services.AddSingleton<IPushOutbox>(outbox);
        }));

        return (app, outbox);
    }

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-r-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var team = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-r-{suffix}",
            AttendanceEnabled = true,
        };
        var venue = new Venue
        {
            Id = Guid.NewGuid(),
            Name = "Karra IP",
            Address = "Idrottsvagen 1, Goteborg",
            Latitude = 57.79,
            Longitude = 11.94,
            IsHome = true,
        };
        var match = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            Type = EventType.Match,
            KickoffUtc = now.AddDays(3),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(team);
        context.Venues.Add(venue);
        context.Events.Add(match);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(trupp.Id, match.Id);
    }

    /// <summary>Ett kallat barn med vårdnadshavare, valfritt med ett svar. Returnerar konto-id:t.</summary>
    private async Task<Guid> SeedInvitedChildAsync(
        Fixture fixture, string tag, Guid callId, Guid teamId, AttendanceReply? reply = null)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var guardian = new Account { Id = Guid.NewGuid(), Email = $"vh-{tag}-{Guid.NewGuid():N}@example.com", CreatedUtc = now };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = fixture.TruppId,
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
        context.AttendanceInvitations.Add(new AttendanceInvitation
        {
            Id = Guid.NewGuid(),
            CallId = callId,
            ChildId = child.Id,
            Reply = reply,
            RespondedByAccountId = reply is null ? null : guardian.Id,
            RespondedUtc = reply is null ? null : now,
        });

        await context.SaveChangesAsync(CancellationToken.None);

        return guardian.Id;
    }

    private async Task<(Guid CallId, Guid TeamId)> SeedCallAsync(Fixture fixture)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();

        var teamId = await context.Events
            .Where(e => e.Id == fixture.EventId)
            .Select(e => e.TeamId)
            .SingleAsync(CancellationToken.None);

        var call = new AttendanceCall
        {
            Id = Guid.NewGuid(),
            MatchId = fixture.EventId,
            OpenedByAccountId = Guid.NewGuid(),
            OpenedUtc = DateTime.UtcNow,
        };
        context.AttendanceCalls.Add(call);
        await context.SaveChangesAsync(CancellationToken.None);

        return (call.Id, teamId);
    }

    private string AdminToken(Guid truppId)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(Guid.NewGuid(), "admin@example.com", new AccountRoles(false, [truppId.ToString()], [])).Token;
    }

    private string PlainToken() =>
        Token(AccountRoles.None);

    private string Token(AccountRoles roles)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();

        return issuer.Issue(Guid.NewGuid(), "konto@example.com", roles).Token;
    }

    private static async Task<HttpResponseMessage> RemindAsync(
        WebApplicationFactory<Program> app, Fixture f, string token)
    {
        using var client = app.CreateClient(ClientOptions);

        var csrfRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/csrf");
        csrfRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var csrfResponse = await client.SendAsync(csrfRequest, CancellationToken.None);
        csrfResponse.EnsureSuccessStatusCode();

        var body = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        var cookie = csrfResponse.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("karra_csrf", StringComparison.Ordinal))
            .Split(';')[0];

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/admin/trupper/{f.TruppId}/events/{f.EventId}/kallelse/remind");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("X-CSRF-TOKEN", body.GetProperty("token").GetString()!);
        request.Headers.Add("Cookie", cookie);

        return await client.SendAsync(request, CancellationToken.None);
    }

    [Fact]
    public async Task Paminnelse_NarBaraDemSomInteSvarat()
    {
        var fixture = await SeedAsync("remind");
        var (callId, teamId) = await SeedCallAsync(fixture);
        await SeedInvitedChildAsync(fixture, "svarat", callId, teamId, AttendanceReply.Coming);
        var notAnswered = await SeedInvitedChildAsync(fixture, "tyst", callId, teamId);
        var (app, outbox) = WithRecordingOutbox();

        var response = await RemindAsync(app, fixture, AdminToken(fixture.TruppId));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(1, body.GetProperty("reminded").GetInt32());

        Assert.True(outbox.Dispatches.TryDequeue(out var dispatch));
        Assert.Equal([notAnswered], dispatch.AccountIds);
        Assert.Contains("Påminnelse", dispatch.Message.Title, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Paminnelse_NarAllaSvarat_KoarIngenNotis()
    {
        var fixture = await SeedAsync("all-answered");
        var (callId, teamId) = await SeedCallAsync(fixture);
        await SeedInvitedChildAsync(fixture, "a", callId, teamId, AttendanceReply.Coming);
        await SeedInvitedChildAsync(fixture, "b", callId, teamId, AttendanceReply.NotComing);
        var (app, outbox) = WithRecordingOutbox();

        var response = await RemindAsync(app, fixture, AdminToken(fixture.TruppId));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None);
        Assert.Equal(0, body.GetProperty("reminded").GetInt32());
        Assert.False(outbox.Dispatches.TryDequeue(out _));
    }

    [Fact]
    public async Task Paminnelse_SomIckeAdmin_Nekas()
    {
        var fixture = await SeedAsync("not-admin");
        var (callId, teamId) = await SeedCallAsync(fixture);
        await SeedInvitedChildAsync(fixture, "tyst", callId, teamId);
        var (app, _) = WithRecordingOutbox();

        var response = await RemindAsync(app, fixture, PlainToken());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
