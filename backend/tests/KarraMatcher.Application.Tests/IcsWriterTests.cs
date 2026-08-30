using System.Text;

using KarraMatcher.Application.Features.Calendar;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Formatreglerna i RFC 5545 är lätta att missa, och en kalenderapp som inte kan tolka
/// feeden säger sällan varför — den visar bara ingenting. Därför testas de var för sig.
/// </summary>
public class IcsWriterTests
{
    private static string[] LinesOf(IcsWriter writer) =>
        writer.ToString().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void AddLine_AvslutarRaderMedCrLf()
    {
        // LF ensamt avvisas av vissa klienter. Testet skrivs mot råsträngen och inte mot
        // Environment.NewLine — CI kör Linux, där det senare är LF.
        var writer = new IcsWriter();

        writer.AddLine("VERSION", "2.0");

        Assert.Equal("VERSION:2.0\r\n", writer.ToString());
    }

    [Fact]
    public void AddLine_KortRad_VikasInte()
    {
        var writer = new IcsWriter();

        writer.AddLine("UID", "abc@karramatcher");

        Assert.Single(LinesOf(writer));
    }

    [Fact]
    public void AddLine_LangRad_VikasVid75Oktetter()
    {
        var writer = new IcsWriter();

        writer.AddLine("SUMMARY", new string('a', 200));

        var lines = LinesOf(writer);

        Assert.True(lines.Length > 1, "Raden borde ha vikts");
        Assert.All(lines, line => Assert.True(
            Encoding.UTF8.GetByteCount(line) <= 75,
            $"Raden är {Encoding.UTF8.GetByteCount(line)} oktetter: {line}"));
    }

    [Fact]
    public void AddLine_VikenRad_BorjarMedBlanksteg()
    {
        // Fortsättningsraden måste inledas med ett blanksteg, annars läses den som en ny
        // egenskap och kalendern får skräp.
        var writer = new IcsWriter();

        writer.AddLine("SUMMARY", new string('a', 200));

        Assert.All(LinesOf(writer).Skip(1), line => Assert.StartsWith(" ", line, StringComparison.Ordinal));
    }

    [Fact]
    public void AddLine_VikningDelarInteEttTecken()
    {
        // Räkningen sker i oktetter, men brytningen måste ske mellan tecken. Ett "ä" är
        // två oktetter i UTF-8, och en brytning inuti det ger obegriplig text — ofta just
        // i ett lagnamn.
        var writer = new IcsWriter();

        writer.AddLine("SUMMARY", string.Concat(Enumerable.Repeat("ä", 120)));

        var joined = string.Concat(LinesOf(writer).Select((line, index) => index == 0 ? line : line[1..]));

        Assert.DoesNotContain('�', joined);
        Assert.Equal("SUMMARY:" + string.Concat(Enumerable.Repeat("ä", 120)), joined);
    }

    [Fact]
    public void AddLine_VikningDelarInteEttSurrogatpar()
    {
        var writer = new IcsWriter();

        writer.AddLine("SUMMARY", string.Concat(Enumerable.Repeat("😀", 60)));

        var joined = string.Concat(LinesOf(writer).Select((line, index) => index == 0 ? line : line[1..]));

        Assert.Equal("SUMMARY:" + string.Concat(Enumerable.Repeat("😀", 60)), joined);
    }

    [Theory]
    [InlineData("Klarebergsvallen, Kärra", "Klarebergsvallen\\, Kärra")]
    [InlineData("Plan 3; ingång B", "Plan 3\\; ingång B")]
    [InlineData("Bakåt\\framåt", "Bakåt\\\\framåt")]
    [InlineData("Rad ett\nRad två", "Rad ett\\nRad två")]
    [InlineData("Rad ett\r\nRad två", "Rad ett\\nRad två")]
    public void EscapeText_EscaparTeckenSomAndrarInnebord(string input, string expected)
    {
        // En adress som "Klarebergsvallen, Kärra" skulle utan escaping tolkas som två
        // värden, och kalendern visa halva adressen.
        Assert.Equal(expected, IcsWriter.EscapeText(input));
    }

    [Fact]
    public void EscapeText_EscaparOmvantSnedstreckForst()
    {
        // Görs det sist escapas de escape-tecken vi själva just lagt till, och resultatet
        // blir dubbelt så många snedstreck som det ska.
        Assert.Equal("a\\\\b\\,c", IcsWriter.EscapeText("a\\b,c"));
    }

    [Fact]
    public void FormatUtc_GerZSuffix()
    {
        var instant = new DateTime(2026, 8, 30, 11, 15, 0, DateTimeKind.Utc);

        Assert.Equal("20260830T111500Z", IcsWriter.FormatUtc(instant));
    }

    [Fact]
    public void FormatUtc_LokalTid_RaknasOmTillUtc()
    {
        // En DateTime med fel Kind ska inte tyst hamna i feeden som om den vore UTC.
        var local = new DateTime(2026, 8, 30, 11, 15, 0, DateTimeKind.Local);

        Assert.Equal(IcsWriter.FormatUtc(local.ToUniversalTime()), IcsWriter.FormatUtc(local));
    }
}
