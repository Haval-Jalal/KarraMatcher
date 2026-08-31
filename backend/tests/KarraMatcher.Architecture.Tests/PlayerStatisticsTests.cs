using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

using KarraMatcher.Api.Diagnostics;
using KarraMatcher.Domain.Common;
using KarraMatcher.Infrastructure.Persistence;

namespace KarraMatcher.Architecture.Tests;

/// <summary>
/// §KM.2 — barnets statistik lämnar aldrig enheten.
///
/// <para>
/// Spelarkortet (matchresultat, mål, assist, märken) lagras uteslutande i familjens egen
/// telefon. Det finns därför ingen tabell, ingen entitet och ingen endpoint för
/// barnstatistik på servern. Det är projektets starkaste integritetsskydd, och det bygger
/// helt på en frånvaro — sådant urholkas tyst om ingen vaktar det.
/// </para>
///
/// <para>
/// Regelverket kräver att bygget <em>faller</em> om någon inför en sådan yta. Testerna här
/// bevakar typnamn och tabeller; motsvarande kontroll av HTTP-ytan finns i
/// <c>PlayerStatisticsEndpointTests</c> bland integrationstesterna.
/// </para>
/// </summary>
public partial class PlayerStatisticsTests
{
    /// <summary>
    /// Ord som beskriver barnstatistik. Matchningen sker på hela PascalCase-ord, aldrig på
    /// delsträngar — annars hade <c>MatchStatus</c> fastnat på "Stat", och en regel som
    /// larmar falskt blir en regel någon stänger av.
    /// </summary>
    private static readonly string[] ForbiddenWords =
    [
        "statistic", "statistics", "statistik", "stat", "stats",
        "goal", "goals", "assist", "assists",
        "badge", "badges", "scorer", "scorers",
        "trophy", "trophies", "spelarkort",
    ];

    /// <summary>
    /// Ordpar som är oskyldiga var för sig men beskriver spelarkortet tillsammans.
    /// "Card" ensamt kan bli ett gult kort någon gång; "PlayerCard" kan det inte.
    /// </summary>
    private static readonly (string First, string Second)[] ForbiddenPairs =
    [
        ("player", "card"),
        ("player", "stat"),
        ("match", "result"),
    ];

    private static readonly Assembly[] BackendAssemblies =
    [
        typeof(IDomainMarker).Assembly,
        typeof(KarraMatcher.Application.DependencyInjection).Assembly,
        typeof(KarraMatcherDbContext).Assembly,
        typeof(HealthChecks).Assembly,
    ];

    [GeneratedRegex(@"[A-Z]+(?![a-z])|[A-Z][a-z0-9]*|[a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex PascalCaseWord();

    internal static string[] SplitWords(string? name) =>
        [.. PascalCaseWord()
            .Matches(name ?? string.Empty)
            .Select(m => m.Value.ToLowerInvariant())];

    /// <summary>
    /// Sant om namnet beskriver barnstatistik. Används både av reglerna nedan och av
    /// självtesterna längst ned, så att detektorn själv är bevisad.
    /// </summary>
    internal static bool NamesPlayerStatistics(string? name)
    {
        var words = SplitWords(name);

        if (words.Any(w => ForbiddenWords.Contains(w, StringComparer.Ordinal)))
        {
            return true;
        }

        for (var i = 0; i < words.Length - 1; i++)
        {
            var here = words[i];
            var next = words[i + 1];

            if (ForbiddenPairs.Any(p =>
                    string.Equals(here, p.First, StringComparison.Ordinal)
                    && next.StartsWith(p.Second, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCompilerGenerated(Type type) =>
        type.Name.StartsWith('<')
        || type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    private static string[] DbSetNames() =>
        [.. typeof(KarraMatcherDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.Name.StartsWith("DbSet", StringComparison.Ordinal))
            .Select(p => p.Name)
            .OrderBy(n => n, StringComparer.Ordinal)];

    [Fact]
    public void Backenden_HarIngenTypSomBeskriverBarnstatistik()
    {
        var offenders = BackendAssemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => !IsCompilerGenerated(t))
            .Where(t => NamesPlayerStatistics(t.Name))
            .Select(t => $"{t.Assembly.GetName().Name}: {t.FullName}")
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"Typer som ser ut att bära barnstatistik:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
                + $"{Environment.NewLine}§KM.2: spelarkortet lagras enbart på enheten. Servern "
                + "ska varken kunna ta emot, lagra eller returnera den datan. Behövs undantaget "
                + "på riktigt krävs ett skrivet beslut i docs/PROJEKT-HANDOFF.md under "
                + "Viktiga beslut — i samma PR.");
    }

    [Fact]
    public void Databasen_HarIngenTabellForBarnstatistik()
    {
        var offenders = typeof(KarraMatcherDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.Name.StartsWith("DbSet", StringComparison.Ordinal))
            .SelectMany(p => p.PropertyType
                .GetGenericArguments()
                .Select(t => t.Name)
                .Append(p.Name)
                .Where(NamesPlayerStatistics)
                .Select(n => $"{nameof(KarraMatcherDbContext)}.{p.Name} ({n})"))
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"DbSet som ser ut att lagra barnstatistik:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => "  - " + o))
                + $"{Environment.NewLine}§KM.2: det ska inte finnas någon tabell för den datan.");
    }

    [Fact]
    public void Databasen_HarPreciseDeTabellerViVantarOss()
    {
        /*
         * Skyddar regeln ovan mot att ga gron for att scannern slutat hitta nagot alls.
         * Laggs en tabell till medvetet ska raden har andras i samma PR -- det ar avsikten.
         *
         * Accounts och RefreshTokens tillkom i #30 och ar medvetet med. Accounts lagrar en
         * vuxens mejladress och ingenting annat; RefreshTokens lagrar hashar och tider.
         * Ingen av dem har nagot falt som kan innehalla en uppgift om ett barn (§KM.1),
         * och spelarkortet nar fortfarande aldrig servern (§KM.2).
         */
        Assert.Equal(
            [
                "Accounts", "AgeGroups", "Clubs", "LoginCodes", "Matches", "RefreshTokens",
                "Teams", "Venues",
            ],
            DbSetNames());
    }

    // ---- Självtester: bevisar att detektorn känner igen rätt saker --------------------

    [Theory]
    [InlineData("PlayerStatistics")]
    [InlineData("PlayerStat")]
    [InlineData("PlayerCard")]
    [InlineData("PlayerCardEntity")]
    [InlineData("GoalTally")]
    [InlineData("AssistDto")]
    [InlineData("MatchResult")]
    [InlineData("MatchResultsController")]
    [InlineData("BadgeAwardedEvent")]
    [InlineData("TopScorerQuery")]
    [InlineData("SpelarkortDto")]
    public void NamesPlayerStatistics_BeskriverBarnstatistik_GerTrue(string name)
    {
        Assert.True(NamesPlayerStatistics(name), $"{name} borde ha fastnat");
    }

    [Theory]
    [InlineData("MatchStatus")]
    [InlineData("Match")]
    [InlineData("Team")]
    [InlineData("Venue")]
    [InlineData("Club")]
    [InlineData("AgeGroup")]
    [InlineData("KarraMatcherDbContext")]
    [InlineData("DatabaseInitializer")]
    [InlineData("CorrelationIdMiddleware")]
    [InlineData("GlobalExceptionHandler")]
    [InlineData("HealthChecks")]
    [InlineData("RateLimiting")]
    [InlineData("StatusCode")]
    public void NamesPlayerStatistics_LegitimtNamn_GerFalse(string name)
    {
        Assert.False(NamesPlayerStatistics(name), $"{name} är ett falskt alarm");
    }

    [Fact]
    public void SplitWords_DelarPascalCaseInklusiveAkronymer()
    {
        Assert.Equal(["match", "status"], SplitWords("MatchStatus"));
        Assert.Equal(["ics", "sequence"], SplitWords("IcsSequence"));
        Assert.Equal(["karra", "matcher", "db", "context"], SplitWords("KarraMatcherDbContext"));
        Assert.Empty(SplitWords(string.Empty));
    }
}
