using System.Globalization;

using KarraMatcher.Domain.Common;

namespace KarraMatcher.Domain.Tests;

public class SwedishTimeTests
{
    // Testet handlar om tidkorrekthet — då får det inte självt bero på maskinens
    // lokalinställningar. CI kör Linux med en annan locale än utvecklarmaskinen.
    private static DateTime Utc(string iso) => DateTime.Parse(
        iso,
        CultureInfo.InvariantCulture,
        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    [Theory]
    // Sommartid (CEST, UTC+2) — hela höstsäsongens matcher ligger här.
    [InlineData("2026-08-29", "14:30", "2026-08-29T12:30:00Z")]
    [InlineData("2026-10-04", "12:00", "2026-10-04T10:00:00Z")]
    // Dagen då klockan ställs tillbaka, 2026-10-25. Eftermiddagen är redan vintertid.
    [InlineData("2026-10-25", "14:30", "2026-10-25T13:30:00Z")]
    // Vintertid (CET, UTC+1) — vårsäsongens tidiga matcher.
    [InlineData("2026-11-15", "14:30", "2026-11-15T13:30:00Z")]
    [InlineData("2027-03-20", "11:00", "2027-03-20T10:00:00Z")]
    public void ToUtc_SvenskLokaltid_GerRattUtc(string date, string time, string expected)
    {
        var result = SwedishTime.ToUtc(
            DateOnly.Parse(date, CultureInfo.InvariantCulture),
            TimeOnly.Parse(time, CultureInfo.InvariantCulture));

        Assert.Equal(Utc(expected), result);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
    }

    [Fact]
    public void ToUtc_TidSomInteFinns_Kastar()
    {
        // 2026-03-29 kl 02.30 hoppas över när klockan ställs fram.
        var ex = Assert.Throws<ArgumentException>(() =>
            SwedishTime.ToUtc(new DateOnly(2026, 3, 29), new TimeOnly(2, 30)));

        Assert.Contains("finns inte", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToUtc_TvetydigTid_ValjerSommartidUtanAttKasta()
    {
        // 2026-10-25 kl 02.30 inträffar två gånger. Vi väljer den första.
        var result = SwedishTime.ToUtc(new DateOnly(2026, 10, 25), new TimeOnly(2, 30));

        Assert.Equal(Utc("2026-10-25T00:30:00Z"), result);
    }

    [Fact]
    public void ToSwedish_ArInversenAvToUtc()
    {
        var utc = SwedishTime.ToUtc(new DateOnly(2026, 9, 13), new TimeOnly(15, 45));

        var local = SwedishTime.ToSwedish(utc);

        Assert.Equal(new DateTime(2026, 9, 13, 15, 45, 0), local);
    }

    [Fact]
    public void ToSwedish_IckeUtc_Kastar()
    {
        Assert.Throws<ArgumentException>(() =>
            SwedishTime.ToSwedish(new DateTime(2026, 9, 13, 15, 45, 0, DateTimeKind.Local)));
    }
}
