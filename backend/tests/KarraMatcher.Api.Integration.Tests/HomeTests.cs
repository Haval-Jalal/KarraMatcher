using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KarraMatcher.Application.Abstractions.Security;
using KarraMatcher.Application.Features.Auth;
using KarraMatcher.Domain.Accounts;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Chat;
using KarraMatcher.Domain.Children;
using KarraMatcher.Domain.Events;
using KarraMatcher.Domain.Teams;
using KarraMatcher.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// Hem-vyns sammanställning (`GET /api/v1/hem`).
///
/// <para>
/// "Allt samlat" för den inloggade: nästa händelse i något av hens lag, kallelser som väntar på
/// svar för hens egna barn, och det senaste i en kanal hen når. Allt scopas server-side till
/// kontots egna medlemskap — en medlem ser bara sitt (§KM.1/§KM.3).
/// </para>
/// </summary>
public sealed class HomeTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private sealed record Fixture(Guid GuardianId, Guid EventId, string TeamSlug);

    private async Task<Fixture> SeedAsync(string suffix)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<KarraMatcherDbContext>();
        var now = DateTime.UtcNow;

        var club = new Club { Id = Guid.NewGuid(), Name = "Karra KIF", Slug = $"klubb-h-{suffix}" };
        var trupp = new AgeGroup { Id = Guid.NewGuid(), ClubId = club.Id, Name = "P2016", Season = "2026" };
        var svart = new Team
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            Name = "Svart",
            ColorHex = "#161616",
            Slug = $"svart-h-{suffix}",
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
            TeamId = svart.Id,
            Type = EventType.Match,
            KickoffUtc = now.AddDays(3),
            OpponentName = "Torslanda",
            VenueId = venue.Id,
            IsHome = true,
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };

        // En redan passerad match ska INTE dyka upp som "nästa".
        var past = new Event
        {
            Id = Guid.NewGuid(),
            TeamId = svart.Id,
            Type = EventType.Match,
            KickoffUtc = now.AddDays(-2),
            OpponentName = "Gammalt",
            VenueId = venue.Id,
            IsHome = true,
            Status = EventStatus.Scheduled,
            UpdatedUtc = now,
        };

        var guardian = new Account
        {
            Id = Guid.NewGuid(),
            Email = $"vh-h-{suffix}@example.com",
            FirstName = "Anna",
            LastName = "Andersson",
            CreatedUtc = now,
        };
        var child = new Child
        {
            Id = Guid.NewGuid(),
            FirstName = "Liam",
            LastInitial = "J",
            AgeGroupId = trupp.Id,
            TeamId = svart.Id,
            CreatedUtc = now,
        };

        // Öppnad kallelse för barnet, ännu obesvarad (Reply == null).
        var call = new AttendanceCall
        {
            Id = Guid.NewGuid(),
            MatchId = match.Id,
            OpenedByAccountId = guardian.Id,
            OpenedUtc = now,
        };
        var invitation = new AttendanceInvitation
        {
            Id = Guid.NewGuid(),
            CallId = call.Id,
            ChildId = child.Id,
        };

        // Ett publicerat meddelande i truppens primärkanal (TeamId == null).
        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            AgeGroupId = trupp.Id,
            TeamId = null,
            AuthorAccountId = guardian.Id,
            Body = "Vi ses på träningen imorgon!",
            CreatedUtc = now,
            PublishAtUtc = now,
            PublishedUtc = now,
        };

        context.Clubs.Add(club);
        context.AgeGroups.Add(trupp);
        context.Teams.Add(svart);
        context.Venues.Add(venue);
        context.Events.AddRange(match, past);
        context.Accounts.Add(guardian);
        context.Children.Add(child);
        context.Guardianships.Add(new Guardianship
        {
            Id = Guid.NewGuid(),
            AccountId = guardian.Id,
            ChildId = child.Id,
            GrantedUtc = now,
        });
        context.AttendanceCalls.Add(call);
        context.AttendanceInvitations.Add(invitation);
        context.ChatMessages.Add(message);
        await context.SaveChangesAsync(CancellationToken.None);

        return new Fixture(guardian.Id, match.Id, svart.Slug);
    }

    private string TokenFor(Guid accountId) =>
        TestAuth.TokenFor(factory.Services, accountId, AccountRoles.None, "konto@example.com");

    private async Task<JsonElement> GetHemAsync(string token)
    {
        using var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/hem");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request, CancellationToken.None);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>(CancellationToken.None)).Clone();
    }

    [Fact]
    public async Task Medlem_SerNastaHandelse_ObesvaradKallelse_OchSenasteChatt()
    {
        var f = await SeedAsync("full");

        var body = await GetHemAsync(TokenFor(f.GuardianId));

        // 1. Nästa händelse: den kommande matchen, inte den passerade.
        var next = body.GetProperty("nextEvent");
        Assert.Equal(f.EventId, next.GetProperty("id").GetGuid());
        Assert.Equal("Torslanda", next.GetProperty("opponent").GetString());
        Assert.Equal(f.TeamSlug, next.GetProperty("teamSlug").GetString());
        Assert.Equal("Karra IP", next.GetProperty("place").GetString());

        // 2. Obesvarad kallelse för det egna barnet, hopslaget per händelse.
        var pending = body.GetProperty("pendingKallelser").EnumerateArray().ToArray();
        Assert.Single(pending);
        Assert.Equal(f.EventId, pending[0].GetProperty("eventId").GetGuid());
        Assert.Equal(1, pending[0].GetProperty("unansweredCount").GetInt32());
        Assert.Equal("Svart", pending[0].GetProperty("teamName").GetString());

        // 3. Senaste i chatten: primärkanalens namn, avsändarens namn och ett utdrag.
        var chat = body.GetProperty("latestChat");
        Assert.Equal("P2016 chatt", chat.GetProperty("channelName").GetString());
        Assert.Equal("Anna Andersson", chat.GetProperty("authorName").GetString());
        Assert.Contains("Vi ses", chat.GetProperty("snippet").GetString(), StringComparison.Ordinal);
        Assert.Equal(JsonValueKind.Null, chat.GetProperty("teamSlug").ValueKind);
    }

    [Fact]
    public async Task UtanMedlemskap_TomSammanstallning()
    {
        // Ett inloggat konto utan medlemskap ser ingenting — sammanställningen scopas till egna
        // medlemskap, inte till att bara vara inloggad (§KM.3).
        var body = await GetHemAsync(TokenFor(Guid.NewGuid()));

        Assert.Equal(JsonValueKind.Null, body.GetProperty("nextEvent").ValueKind);
        Assert.Empty(body.GetProperty("pendingKallelser").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("latestChat").ValueKind);
    }
}
