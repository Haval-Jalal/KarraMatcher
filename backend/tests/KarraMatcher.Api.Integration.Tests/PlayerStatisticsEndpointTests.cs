using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace KarraMatcher.Api.Integration.Tests;

/// <summary>
/// §KM.2 — ingen endpoint tar emot eller returnerar barnstatistik.
///
/// <para>
/// Systertestet <c>PlayerStatisticsTests</c> bland arkitekturtesterna bevakar typnamn och
/// tabeller. Det här testet tittar på den yta som faktiskt är exponerad mot internet:
/// routerna i den startade appen. En endpoint kan införas utan att någon ny typ skapas —
/// en minimal-API-rad räcker — och då är det bara den här kontrollen som fångar den.
/// </para>
///
/// <para>
/// Ordlistan hålls medvetet skild från arkitekturtesternas. Den här matchar
/// URL-segment (gemener, bindestreck), den andra matchar PascalCase-typnamn. Att slå ihop
/// dem hade krävt en referens mellan två testprojekt för att spara tio strängar.
/// </para>
/// </summary>
public class PlayerStatisticsEndpointTests(KarraMatcherApiFactory factory)
    : IClassFixture<KarraMatcherApiFactory>
{
    private static readonly string[] ForbiddenSegments =
    [
        "stat", "stats", "statistic", "statistics", "statistik",
        "goal", "goals", "assist", "assists",
        "badge", "badges", "scorer", "scorers",
        "trophy", "trophies",
        "playercard", "player-card", "playercards", "player-cards",
        "spelarkort",
    ];

    /// <summary>
    /// Delar upp ett routemönster i jämförbara segment. Parameterhållare som
    /// <c>{id}</c> är inte namn på resurser och tas bort.
    /// </summary>
    internal static string[] SegmentsOf(string? pattern) =>
        [.. (pattern ?? string.Empty)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !s.StartsWith('{'))
            .Select(s => s.ToLowerInvariant())];

    internal static bool LooksLikePlayerStatistics(string? pattern) =>
        SegmentsOf(pattern).Any(s => ForbiddenSegments.Contains(s, StringComparer.Ordinal));

    private string[] RoutePatterns() =>
        [.. factory.Services
            .GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText ?? string.Empty)];

    [Fact]
    public void IngenEndpoint_ExponerarBarnstatistik()
    {
        var offenders = RoutePatterns().Where(LooksLikePlayerStatistics).ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Endpoints som ser ut att röra barnstatistik:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
                + $"{Environment.NewLine}§KM.2: spelarkortet lagras enbart på enheten och får "
                + "varken tas emot eller returneras av servern. Införs undantaget medvetet "
                + "krävs ett skrivet beslut i docs/PROJEKT-HANDOFF.md — i samma PR.");
    }

    [Fact]
    public void Routetabellen_GarAttLasa()
    {
        // Skyddar regeln ovan mot att gå grön för att inga routes hittades alls.
        var patterns = RoutePatterns();

        Assert.NotEmpty(patterns);
        Assert.Contains("/health", patterns, StringComparer.Ordinal);
    }

    // ---- Självtester: bevisar att detektorn faller på rätt mönster --------------------

    [Theory]
    [InlineData("/api/v1/players/{id}/stats")]
    [InlineData("/api/v1/players/{id}/statistics")]
    [InlineData("/api/v1/player-cards")]
    [InlineData("/api/v1/playercards/{id}")]
    [InlineData("/api/v1/teams/{teamId}/goals")]
    [InlineData("/api/v1/badges")]
    [InlineData("/api/v1/spelarkort")]
    public void LooksLikePlayerStatistics_Forbjudet_GerTrue(string pattern)
    {
        Assert.True(LooksLikePlayerStatistics(pattern), $"{pattern} borde ha fastnat");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/")]
    [InlineData("/api/v1/teams/{teamId}/matches")]
    [InlineData("/api/v1/matches/{id}")]
    [InlineData("/api/v1/venues")]
    [InlineData("/api/v1/teams/{teamId}/calendar.ics")]
    public void LooksLikePlayerStatistics_Legitim_GerFalse(string pattern)
    {
        Assert.False(LooksLikePlayerStatistics(pattern), $"{pattern} är ett falskt alarm");
    }

    [Fact]
    public void SegmentsOf_TarBortParametrarOchTommaSegment()
    {
        Assert.Equal(["api", "v1", "matches"], SegmentsOf("/api/v1/matches/{id}"));
        Assert.Empty(SegmentsOf("/"));
        Assert.Empty(SegmentsOf(null));
    }
}
