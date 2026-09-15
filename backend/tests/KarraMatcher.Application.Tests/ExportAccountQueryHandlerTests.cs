using KarraMatcher.Application.Abstractions.Persistence;
using KarraMatcher.Application.Features.Auth.ExportAccount;
using KarraMatcher.Domain.Attendance;
using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Registerutdraget (`#67`, checklistan 9.13).
///
/// <para>
/// Två saker vaktas här. Att utdraget faktiskt bär det kontot äger, översatt så en människa
/// kan läsa det. Och att spelarkortet nämns rakt ut som frånvarande — det är kravet som gör
/// utdraget ärligt i stället för att tiga om ett tomrum (§KM.2).
/// </para>
/// </summary>
public class ExportAccountQueryHandlerTests
{
    private static readonly DateTime ExportMoment = new(2026, 10, 3, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Kickoff = new(2026, 10, 25, 11, 0, 0, DateTimeKind.Utc);

    private static readonly Guid Account = Guid.NewGuid();

    private static (ExportAccountQueryHandler Handler, FakeAccountExportRepository Repository) Build()
    {
        var repository = new FakeAccountExportRepository();

        return (new ExportAccountQueryHandler(repository, new FakeClock(ExportMoment)), repository);
    }

    private static AccountExportData DataWith(
        IReadOnlyList<CarpoolOfferExportRow>? offers = null,
        IReadOnlyList<CarpoolRequestExportRow>? requests = null,
        IReadOnlyList<AttendanceResponseExportRow>? attendance = null,
        IReadOnlyList<NotificationPreferenceExportRow>? preferences = null,
        IReadOnlyList<PushSubscriptionExportRow>? subscriptions = null) =>
        new(
            new AccountExportRow("foralder@example.com", "Anna", "Berg", Kickoff.AddYears(-1), Kickoff),
            offers ?? [],
            requests ?? [],
            attendance ?? [],
            preferences ?? [],
            subscriptions ?? []);

    [Fact]
    public async Task HandleAsync_OkantKonto_GerNull()
    {
        var (handler, _) = Build();

        var result = await handler.HandleAsync(
            new ExportAccountQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_BarKontotsEgnaUppgifter()
    {
        var (handler, repository) = Build();
        repository.Set(Account, DataWith());

        var result = await handler.HandleAsync(new ExportAccountQuery(Account), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("foralder@example.com", result.Account.Email);
        Assert.Equal("Anna", result.Account.FirstName);
        Assert.Equal("Berg", result.Account.LastName);
    }

    [Fact]
    public async Task HandleAsync_SatterExporteradTidFranKlockan()
    {
        var (handler, repository) = Build();
        repository.Set(Account, DataWith());

        var result = await handler.HandleAsync(new ExportAccountQuery(Account), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(ExportMoment, result.ExportedUtc);
    }

    [Fact]
    public async Task HandleAsync_OversatterEnumTillLasbaraNamn()
    {
        // Ett utdrag fullt av "0" och "2" är inte läsbart för en människa. Enum blir namn,
        // som klienten sedan kan visa på svenska.
        var (handler, repository) = Build();
        repository.Set(Account, DataWith(
            offers:
            [
                new CarpoolOfferExportRow(
                    "Torslanda IK", Kickoff, CarpoolDirection.Both, "Kärra centrum",
                    Kickoff.AddHours(-1), 3, "Har plats för en till", CarpoolOfferStatus.Open, Kickoff),
            ],
            attendance:
            [
                new AttendanceResponseExportRow(
                    "Torslanda IK", Kickoff, AttendanceStatus.Coming, 2, Kickoff),
            ]));

        var result = await handler.HandleAsync(new ExportAccountQuery(Account), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Both", result.CarpoolOffers[0].Direction);
        Assert.Equal("Open", result.CarpoolOffers[0].Status);
        Assert.Equal("Torslanda IK", result.CarpoolOffers[0].MatchOpponent);
        Assert.Equal("Coming", result.AttendanceResponses[0].Status);
    }

    [Fact]
    public async Task HandleAsync_NamnerSpelarkortetSomFranvarande()
    {
        // §KM.2: kortet finns inte på servern och kan inte lämnas ut. Utdraget ska säga det
        // rakt ut och peka på säkerhetskopieringskoden, inte tiga om tomrummet.
        var (handler, repository) = Build();
        repository.Set(Account, DataWith());

        var result = await handler.HandleAsync(new ExportAccountQuery(Account), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("telefon", result.PlayerCard.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "säkerhetskopieringskoden",
            result.PlayerCard.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HandleAsync_TomtKonto_GerTommaListorInteNull()
    {
        // Ett konto som bara loggat in ska ge ett giltigt, tomt utdrag — inte null-listor
        // som klienten kraschar på.
        var (handler, repository) = Build();
        repository.Set(Account, DataWith());

        var result = await handler.HandleAsync(new ExportAccountQuery(Account), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.CarpoolOffers);
        Assert.Empty(result.CarpoolRequests);
        Assert.Empty(result.AttendanceResponses);
        Assert.Empty(result.NotificationSettings);
        Assert.Empty(result.PushSubscriptions);
    }

    [Fact]
    public async Task HandleAsync_PrenumerationBarLagUtanTekniskAdress()
    {
        // Prenumerationen tas med som "du får notiser för Gul", men endpoint och nycklar är
        // secrets (§KM.10) och finns inte ens som fält på DTO:n.
        var (handler, repository) = Build();
        repository.Set(Account, DataWith(
            subscriptions: [new PushSubscriptionExportRow("Gul", Kickoff, Kickoff.AddDays(1))]));

        var result = await handler.HandleAsync(new ExportAccountQuery(Account), CancellationToken.None);

        Assert.NotNull(result);
        var subscription = Assert.Single(result.PushSubscriptions);
        Assert.Equal("Gul", subscription.TeamName);
    }
}
