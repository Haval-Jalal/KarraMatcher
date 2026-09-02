using System.Globalization;

using KarraMatcher.Application.Features.Matches.Import;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Parsern för massinlägg (`#38`, checklistan 6.3).
///
/// <para>
/// <b>Det här är funktionen som avgör om tränarna stannar.</b> Att knappa in tjugofem
/// matcher för hand gör ingen två gånger, och en app utan matcher är en app ingen öppnar
/// igen. Därför prövas den mot text som ser ut som det en tränare faktiskt klistrar in —
/// inte mot en form vi hittat på och sedan skrivit en parser för.
/// </para>
///
/// <para>
/// Indata kommer från urklipp, alltså från ett system vi inte styr över. Den får aldrig
/// kasta: en rad som inte går att tolka blir ett besked, och resten tolkas ändå.
/// </para>
/// </summary>
public sealed class ScheduleParserTests
{
    private static readonly Guid Klareberg = Guid.NewGuid();
    private static readonly Guid Fjardingsplan = Guid.NewGuid();

    /// <summary>Världen som den ser ut för parsern: lagen och spelplatserna som finns.</summary>
    private static ScheduleContext Context() => new(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["kärra kif p2016 gul"] = "gul",
            ["kärra kif p2016 blå"] = "bla",
        },
        new Dictionary<string, Guid>(StringComparer.Ordinal)
        {
            ["klarebergsvallen 3"] = Klareberg,
            ["fjärdingsplan 11"] = Fjardingsplan,
        },
        new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        // Svensk sommartid: 15:30 blir 13:30 UTC. Omrakningen sker pa ett stalle i
        // appen -- har matas den in, sa parsern kan provas utan tidszonsberoende.
        local => DateTime.TryParse(local, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed.AddHours(-2).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
            : null);

    // ---- Verklig inklistrad text -----------------------------------------------------

    [Fact]
    public void Parse_TabbseparateratFranKalkylark_TolkasHelt()
    {
        /*
         * Formen kommer fran projektets egen startdata, som i sin tur kommer fran det
         * riktiga serieschemat. Tabb ar vad ett kalkylark klistrar in.
         */
        const string pasted =
            "Datum\tTid\tLag\tMotståndare\tPlats\n" +
            "2026-08-30\t13:15\tKärra KIF P2016 Gul\tFinlandia Pallo AIF Blå\tKlarebergsvallen 3\n" +
            "2026-09-05\t15:30\tKärra KIF P2016 Gul\tLundby IF P2016 Grön\tFjärdingsplan 11\n";

        var lines = ScheduleParser.Parse(pasted, Context());

        Assert.Equal(LineOutcome.Skipped, lines[0].Outcome);
        Assert.Equal(LineOutcome.Ok, lines[1].Outcome);
        Assert.Equal(LineOutcome.Ok, lines[2].Outcome);
        var match = lines[1].Match;

        Assert.NotNull(match);
        Assert.Equal("Finlandia Pallo AIF Blå", match.Opponent);
        Assert.Equal("gul", match.TeamSlug);
        Assert.Equal(Klareberg, match.VenueId);
    }

    [Theory]
    [InlineData(';')]
    [InlineData(',')]
    public void Parse_KlararSemikolonOchKomma(char separator)
    {
        var line = string.Join(
            separator,
            "2026-08-30", "13:15", "Kärra KIF P2016 Gul", "Torslanda IK", "Klarebergsvallen 3");

        var parsed = ScheduleParser.Parse(line, Context());

        Assert.Equal(LineOutcome.Ok, Assert.Single(parsed).Outcome);
    }

    [Fact]
    public void Parse_TabbVinnerOverKomma()
    {
        /*
         * "Kareby IS, Bla" innehaller ett komma. Gissar parsern komma delas lagnamnet i
         * tva falt och raden blir obegriplig -- darfor provas tabb forst.
         */
        const string line =
            "2026-08-30\t13:15\tKärra KIF P2016 Gul\tKareby IS, Blå\tKlarebergsvallen 3";

        var parsed = Assert.Single(ScheduleParser.Parse(line, Context()));

        Assert.Equal(LineOutcome.Ok, parsed.Outcome);
        Assert.Equal("Kareby IS, Blå", parsed.Match!.Opponent);
    }

    // ---- Datum och tid ---------------------------------------------------------------

    [Theory]
    [InlineData("2026-09-05", "2026-09-05")]
    [InlineData("2026/09/05", "2026-09-05")]
    public void ParseDate_KlararIsoFormat(string input, string expected)
    {
        Assert.Equal(expected, ScheduleParser.ParseDate(input, new DateOnly(2026, 8, 1)));
    }

    [Theory]
    [InlineData("5 sep")]
    [InlineData("5 september")]
    [InlineData("5/9")]
    public void ParseDate_KlararSvenskaFormatUtanArtal(string input)
    {
        // Ett kopierat schema saknar ofta årtal. Då menas den säsong som kommer.
        Assert.Equal("2026-09-05", ScheduleParser.ParseDate(input, new DateOnly(2026, 8, 1)));
    }

    [Fact]
    public void ParseDate_UtanArtal_VaijerNastaForekomst()
    {
        // Klistras schemat in i december menas januari nästa år, inte januari som var.
        Assert.Equal("2027-01-10", ScheduleParser.ParseDate("10 jan", new DateOnly(2026, 12, 1)));
    }

    [Theory]
    [InlineData("13:15", "13:15")]
    [InlineData("13.15", "13:15")]
    [InlineData("9:05", "09:05")]
    public void ParseTime_KlararKolonOchPunkt(string input, string expected)
    {
        Assert.Equal(expected, ScheduleParser.ParseTime(input));
    }

    [Theory]
    [InlineData("i morgon")]
    [InlineData("32 sep")]
    [InlineData("5 hurtsdag")]
    [InlineData("")]
    public void ParseDate_AvvisarSkrap(string input)
    {
        Assert.Null(ScheduleParser.ParseDate(input, new DateOnly(2026, 8, 1)));
    }

    // ---- Besked per rad --------------------------------------------------------------

    [Fact]
    public void Parse_RadMedForFaFalt_SagerVadSomSaknas()
    {
        // "Något gick fel" hjälper ingen. Tränaren ska se vilken rad och varför.
        var parsed = Assert.Single(ScheduleParser.Parse("2026-08-30\t13:15\tKärra KIF P2016 Gul", Context()));

        Assert.Equal(LineOutcome.Incomplete, parsed.Outcome);
        // Beskedet ska namnge både vad raden hade och vad som krävs.
        Assert.Contains("3 fält", parsed.Problem, StringComparison.Ordinal);
        Assert.Contains("behöver 5", parsed.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_OkantLag_PekasUt()
    {
        const string line = "2026-08-30\t13:15\tNågot Annat Lag\tTorslanda IK\tKlarebergsvallen 3";

        var parsed = Assert.Single(ScheduleParser.Parse(line, Context()));

        Assert.Equal(LineOutcome.UnknownReference, parsed.Outcome);
        Assert.Contains("Något Annat Lag", parsed.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_OkandSpelplats_SagerAttDenBorLaggasUppForst()
    {
        const string line = "2026-08-30\t13:15\tKärra KIF P2016 Gul\tTorslanda IK\tHittepåplan";

        var parsed = Assert.Single(ScheduleParser.Parse(line, Context()));

        Assert.Equal(LineOutcome.UnknownReference, parsed.Outcome);
        Assert.Contains("Lägg upp den först", parsed.Problem, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_SammaMatchTvaGanger_MarkerasSomDubblett()
    {
        // Ett schema inklistrat två gånger är ett vanligare misstag än man tror.
        const string line = "2026-08-30\t13:15\tKärra KIF P2016 Gul\tTorslanda IK\tKlarebergsvallen 3";

        var parsed = ScheduleParser.Parse($"{line}\n{line}", Context());

        Assert.Equal(LineOutcome.Ok, parsed[0].Outcome);
        Assert.Equal(LineOutcome.Duplicate, parsed[1].Outcome);
    }

    [Fact]
    public void Parse_MatchSomRedanFinns_MarkerasSomDubblett()
    {
        const string line = "2026-08-30\t13:15\tKärra KIF P2016 Gul\tTorslanda IK\tKlarebergsvallen 3";

        // Nyckeln som importen redan känner till, i samma form parsern bygger den.
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "gul|2026-08-30T11:15:00Z|torslanda ik",
        };

        var context = Context() with { ExistingMatchKeys = existing };

        Assert.Equal(LineOutcome.Duplicate, Assert.Single(ScheduleParser.Parse(line, context)).Outcome);
    }

    // ---- Kraschar aldrig -------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\n\n")]
    [InlineData("bara en rad utan avgränsare")]
    [InlineData("\t\t\t\t")]
    [InlineData("<script>alert(1)</script>\t13:15\tx\ty\tz")]
    [InlineData("2026-08-30\t13:15\t\t\t")]
    public void Parse_TrasigIndata_KastarAldrig(string input)
    {
        // Indata kommer från urklipp, alltså från ett system vi inte styr över.
        var parsed = ScheduleParser.Parse(input, Context());

        Assert.All(parsed, line => Assert.NotEqual(LineOutcome.Ok, line.Outcome));
    }

    [Fact]
    public void Parse_EnormInklistring_StannarVidTaket()
    {
        /*
         * En sasong ar trettio rader. Taket finns for att en inklistring pa tio megabyte
         * inte ska binda upp servern -- Render har en instans (checklistan 6.3).
         */
        var huge = string.Join('\n', Enumerable.Repeat("skräp", ScheduleParser.MaxLines * 3));

        Assert.Equal(ScheduleParser.MaxLines, ScheduleParser.Parse(huge, Context()).Count);
    }

    [Fact]
    public void Parse_Null_GerTomtResultat()
    {
        Assert.Empty(ScheduleParser.Parse(null, Context()));
    }
}
