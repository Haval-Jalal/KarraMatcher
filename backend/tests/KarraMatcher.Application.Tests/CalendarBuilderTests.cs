using System.Text;

using KarraMatcher.Application.Features.Calendar;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// ICS-byggaren (RFC 5545). Ren funktion — prövas rad för rad utan databas.
///
/// <para>
/// Vaktar att feeden är läsbar för en kalender-app: rätt struktur, UTC-tider, escapad fritext,
/// vikta rader — och att en inställd händelse märks som inställd, inte tas bort.
/// </para>
/// </summary>
public sealed class CalendarBuilderTests
{
    private static readonly DateTimeOffset Kickoff =
        new(2026, 9, 20, 12, 0, 0, TimeSpan.Zero);

    private static CalendarEventEntry Entry(
        string summary = "Gul – Hemma mot Torslanda",
        string location = "Karra IP",
        bool cancelled = false) =>
        new(
            Uid: "abc@karramatcher",
            StartUtc: Kickoff,
            EndUtc: Kickoff.AddHours(2),
            Summary: summary,
            Location: location,
            Cancelled: cancelled,
            Sequence: 0,
            StampUtc: Kickoff);

    [Fact]
    public void Build_GerEnGiltigVevent_MedUtcTider()
    {
        var ics = CalendarBuilder.Build("Kärra Matcher", [Entry()]);

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("BEGIN:VEVENT\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("UID:abc@karramatcher\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTSTART:20260920T120000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("DTEND:20260920T140000Z\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("SUMMARY:Gul – Hemma mot Torslanda\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("LOCATION:Karra IP\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("STATUS:CONFIRMED\r\n", ics, StringComparison.Ordinal);
        Assert.Contains("END:VEVENT\r\n", ics, StringComparison.Ordinal);
        Assert.EndsWith("END:VCALENDAR\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_MarkerarInstalldHandelse()
    {
        var ics = CalendarBuilder.Build("Kärra Matcher", [Entry(cancelled: true)]);

        Assert.Contains("STATUS:CANCELLED\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_EscaparFritext()
    {
        var ics = CalendarBuilder.Build("Kärra Matcher", [Entry(summary: "Cup, dag 1; grupp A")]);

        // Komma och semikolon escapas (RFC 5545 §3.3.11), annars bryts fältet isär.
        Assert.Contains("SUMMARY:Cup\\, dag 1\\; grupp A\r\n", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_UtelamnarTomPlats()
    {
        var ics = CalendarBuilder.Build("Kärra Matcher", [Entry(location: "")]);

        Assert.DoesNotContain("LOCATION:", ics, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_VikerLangaRader_TillHogst75Oktetter()
    {
        var ics = CalendarBuilder.Build(
            "Kärra Matcher",
            [Entry(summary: new string('x', 200))]);

        foreach (var line in ics.Split("\r\n"))
        {
            Assert.True(
                Encoding.UTF8.GetByteCount(line) <= 75,
                $"Raden är {Encoding.UTF8.GetByteCount(line)} oktetter: {line}");
        }
    }
}
