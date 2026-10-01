using KarraMatcher.Application.Features.Carpool;
using KarraMatcher.Domain.Carpool;

namespace KarraMatcher.Application.Tests;

/// <summary>
/// Erbjudandets validator (§KM.12). Avgångstiden lagras i UTC (§KM.5) — en icke-UTC-tid ska
/// avvisas som 400, inte 500:a i Npgsql (`#492`).
/// </summary>
public class CarpoolOfferDraftValidatorTests
{
    private readonly CarpoolOfferDraftValidator _validator = new();

    private static CarpoolOfferDraft Valid() => new(
        CarpoolDirection.Both,
        "Kärra centrum",
        new DateTime(2026, 10, 10, 9, 0, 0, DateTimeKind.Utc),
        2,
        null);

    [Fact]
    public void GiltigtErbjudande_ArGiltigt()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void IckeUtcAvgang_ArOgiltig(DateTimeKind kind)
    {
        var draft = Valid() with { DepartureUtc = new DateTime(2026, 10, 10, 9, 0, 0, kind) };

        Assert.False(_validator.Validate(draft).IsValid);
    }
}
